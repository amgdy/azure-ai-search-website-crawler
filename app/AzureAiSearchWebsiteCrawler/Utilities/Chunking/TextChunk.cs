namespace AzureAiSearchWebsiteCrawler.Utilities.Chunking;

public record TextChunk(int PageNumber, string Text)
{
    public string Hash => $"{PageNumber}-{Text}".ComputeSha256Hash();
};