namespace AzureAiSearchWebsiteCrawler.Models;


public record CrawledWebPage(Uri Url, string Title, string Content);
