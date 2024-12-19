namespace AzureAiSearchWebsiteCrawler.Utilities.Chunking;

public interface ITextSplitter
{
    /// <summary>
    /// Splits a list of text pages into smaller chunks.
    /// </summary>
    /// <param name="textPages">The text pages to split.</param>
    /// <returns>A generator of text chunks out of text pages.</returns>
    public IEnumerable<TextChunk> SplitTextPages(List<TextPage> textPages);
}
