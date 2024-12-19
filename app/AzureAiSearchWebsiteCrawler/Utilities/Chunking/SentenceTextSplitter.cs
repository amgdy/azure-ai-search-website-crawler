using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;

namespace AzureAiSearchWebsiteCrawler.Utilities.Chunking;

public class SentenceTextSplitter(ILogger<SentenceTextSplitter> logger, IOptions<AzureOpenAiOptions> azureOpenAiOptions, IOptions<TextSplitterOptions> textSplitterOptions) : ITextSplitter
{
    private static readonly List<char> SentenceEndings =
    [
        '.', '!', '?', '。', '！', '？', '‼', '⁇', '⁈', '⁉'
    ];

    private static readonly List<char> WordBreaks =
    [
        ',', ';', ':', ' ', '(', ')', '[', ']', '{', '}', '\t', '\n',
        '、', '，', '；', '：', '（', '）', '【', '】', '「', '」', '『', '』', '〔', '〕', '〈', '〉', '《', '》', '〖', '〗', '〘', '〙', '〚', '〛', '〝', '〞', '〟', '〰', '–', '—', '‘', '’', '‚', '‛', '“', '”', '„', '‟', '‹', '›'
    ];

    private readonly TextSplitterOptions _textSplitterOptions = textSplitterOptions.Value;
    private readonly int _sectionOverlap = (int)(textSplitterOptions.Value.DefaultSectionLength * textSplitterOptions.Value.DefaultOverlapPercent / 100.0);
    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForModel(azureOpenAiOptions.Value.EmbeddingModelDeployment);

    public IEnumerable<TextChunk> SplitTextPages(List<TextPage> textPages)
    {
        int FindPage(int offset)
        {
            for (int i = 0; i < textPages.Count - 1; i++)
            {
                if (offset >= textPages[i].Offset && offset < textPages[i + 1].Offset)
                {
                    return textPages[i].PageNumber;
                }
            }

            return textPages[^1].PageNumber;
        }

        var allText = string.Concat(textPages.Select(p => p.Text));
        if (string.IsNullOrWhiteSpace(allText))
        {
            yield break;
        }

        if (allText.Length <= _textSplitterOptions.DefaultSectionLength)
        {
            foreach (var textChunk in SplitPageByMaxTokens(FindPage(0), allText))
            {
                yield return textChunk;
            }

            yield break;
        }

        int start = 0;
        while (start + _sectionOverlap < allText.Length)
        {
            int end = start + _textSplitterOptions.DefaultSectionLength;
            if (end > allText.Length)
            {
                end = allText.Length;
            }
            else
            {
                int lastWord = -1;
                while (end < allText.Length && (end - start - _textSplitterOptions.DefaultSectionLength) < _textSplitterOptions.SentenceSearchLimit && !SentenceEndings.Contains(allText[end]))
                {
                    if (WordBreaks.Contains(allText[end]))
                        lastWord = end;
                    end++;
                }
                if (end < allText.Length && !SentenceEndings.Contains(allText[end]) && lastWord > 0)
                    end = lastWord;
            }

            if (end < allText.Length)
            {
                end++;
            }

            int tempStart = start;
            int lastWordStart = -1;
            while (tempStart > 0 && tempStart > end - _textSplitterOptions.DefaultSectionLength - 2 * _textSplitterOptions.SentenceSearchLimit && !SentenceEndings.Contains(allText[tempStart]))
            {
                if (WordBreaks.Contains(allText[tempStart]))
                {
                    lastWordStart = tempStart;
                }

                tempStart--;
            }
            if (!SentenceEndings.Contains(allText[tempStart]) && lastWordStart > 0)
            {
                tempStart = lastWordStart;
            }
            if (tempStart > 0)
            {
                tempStart++;
            }

            var sectionText = allText.Substring(tempStart, end - tempStart);
            foreach (var textChunk in SplitPageByMaxTokens(FindPage(tempStart), sectionText))
                yield return textChunk;

            int lastTableStart = sectionText.LastIndexOf("<table");
            if (lastTableStart > 2 * _textSplitterOptions.SentenceSearchLimit && lastTableStart > sectionText.LastIndexOf("</table"))
            {
                logger.LogInformation("Section ends with unclosed table, starting next section with the table at page {PageStart} offset {TempStart} table start {LastTableStart}", FindPage(tempStart), tempStart, lastTableStart);
                start = Math.Min(end - _sectionOverlap, tempStart + lastTableStart);
            }
            else
            {
                start = end - _sectionOverlap;
            }
        }

        if (start + _sectionOverlap < allText.Length)
        {
            foreach (var textChunk in SplitPageByMaxTokens(FindPage(start), allText.Substring(start)))
                yield return textChunk;
        }
    }

    private IEnumerable<TextChunk> SplitPageByMaxTokens(int pageNum, string text)
    {
        var tokensCount = _tokenizer.CountTokens(text);
        if (tokensCount <= _textSplitterOptions.MaxTokensPerSection)
        {
            yield return new TextChunk(pageNum, text);
        }
        else
        {
            int length = text.Length;
            int start = length / 2;
            int pos = 0;
            int boundary = length / 3;
            int splitPosition = -1;

            while (start - pos > boundary)
            {
                if (SentenceEndings.Contains(text[start - pos]))
                {
                    splitPosition = start - pos;
                    break;
                }
                else if (SentenceEndings.Contains(text[start + pos]))
                {
                    splitPosition = start + pos;
                    break;
                }
                pos++;
            }

            string firstHalf;
            string secondHalf;
            if (splitPosition > 0)
            {
                firstHalf = text.Substring(0, splitPosition + 1);
                secondHalf = text.Substring(splitPosition + 1);
            }
            else
            {
                int middle = length / 2;
                int overlap = (int)(length * (_textSplitterOptions.DefaultOverlapPercent / 100.0));
                firstHalf = text.Substring(0, middle + overlap);
                secondHalf = text.Substring(middle - overlap);
            }

            foreach (var textChunk in SplitPageByMaxTokens(pageNum, firstHalf))
            {
                yield return textChunk;
            }

            foreach (var textChunk in SplitPageByMaxTokens(pageNum, secondHalf))
            {
                yield return textChunk;
            }
        }
    }
}
