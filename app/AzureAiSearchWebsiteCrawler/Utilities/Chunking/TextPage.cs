namespace AzureAiSearchWebsiteCrawler.Utilities.Chunking;

public record TextPage(int PageNumber, int Offset, string Text)
{
    public string Hash => $"{PageNumber}-{Offset}-{Text}".ComputeSha256Hash();
};
