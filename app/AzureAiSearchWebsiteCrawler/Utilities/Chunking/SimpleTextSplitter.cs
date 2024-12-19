namespace AzureAiSearchWebsiteCrawler.Utilities.Chunking;

public class SimpleTextSplitter(int maxObjectLength = 1000) : ITextSplitter
{
    public IEnumerable<TextChunk> SplitTextPages(List<TextPage> pages)
    {
        var allText = string.Concat(pages.Select(p => p.Text));
        if (string.IsNullOrWhiteSpace(allText))
        {
            yield break;
        }

        for (int i = 0; i < allText.Length; i += maxObjectLength)
        {
            var textSegment = allText.Substring(i, Math.Min(maxObjectLength, allText.Length - i));
            yield return new TextChunk(i / maxObjectLength, textSegment);
        }
    }
}
