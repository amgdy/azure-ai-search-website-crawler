using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace AzureAiSearchWebsiteCrawler.Services;
public class WebCrawlerService(ILogger<WebCrawlerService> logger,
    IOptions<WebCrawlerOptions> options,
    BlockingCollection<WebPageContent> processingQueue)
{
    private CrawlConfiguration CreateCrawlConfiguration()
    {
        return new CrawlConfiguration
        {
            CrawlTimeoutSeconds = 300,
            MaxConcurrentThreads = 10,
            MinCrawlDelayPerDomainMilliSeconds = 100,
            IsSslCertificateValidationEnabled = true,
            MaxPagesToCrawl = options.Value.MaxPagesToCrawl,
            MaxCrawlDepth = options.Value.MaxCrawlDepth,
            MaxRetryCount = options.Value.MaxRetryAttempts,
            UserAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.2903.86"
        };
    }

    private static int totalCrawledPages = 0;

    public async Task StartCrawlAsync()
    {
        logger.LogInformation("Starting web crawl of {Url}", options.Value.Url);
        var crawlConfig = CreateCrawlConfiguration();
        var crawler = new PoliteWebCrawler(crawlConfig);
        crawler.PageCrawlStarting += OnPageCrawlStarting;
        crawler.PageCrawlCompleted += OnPageCrawlCompleted;
        crawler.PageLinksCrawlDisallowed += (sender, e) =>
        {
            logger.LogInformation("Did not crawl the links on page {Uri} due to {Reason}", e.CrawledPage.Uri.AbsoluteUri, e.DisallowedReason);
        };

        var result = await crawler.CrawlAsync(new Uri(options.Value.Url));

        if (result.ErrorOccurred)
        {
            logger.LogError("Crawl of {RootUri} ({TotalCrawledPages} pages) completed with error: {ErrorMessage}", result.RootUri.AbsoluteUri, totalCrawledPages, result.ErrorException.Message);
        }
        else
        {
            logger.LogInformation("Crawl of {RootUri} ({TotalCrawledPages} pages) completed without error.", result.RootUri.AbsoluteUri, totalCrawledPages);
        }
    }

    private void OnPageCrawlStarting(object? sender, PageCrawlStartingArgs e)
    {
        var pageToCrawl = e.PageToCrawl;
        logger.LogInformation("Starting crawl of {Uri} found on {ParentUri}, Depth: {CrawlDepth}", pageToCrawl.Uri.AbsoluteUri, pageToCrawl.ParentUri.AbsoluteUri, pageToCrawl.CrawlDepth);
    }

    private void OnPageCrawlCompleted(object? sender, PageCrawlCompletedArgs e)
    {
        var crawledPage = e.CrawledPage;
        string uri = crawledPage.Uri.AbsoluteUri;

        if (crawledPage.HttpRequestException != null || crawledPage.HttpResponseMessage?.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Crawl of page failed {Uri}: exception '{ExceptionMessage}', response status {StatusCode}", uri, crawledPage.HttpRequestException?.Message, crawledPage.HttpResponseMessage?.StatusCode);
            return;
        }

        if (string.IsNullOrEmpty(crawledPage.Content.Text))
        {
            logger.LogInformation("Page had no content {Uri}", uri);
            return;
        }
        else
        {
            logger.LogInformation("Crawled page {Uri} with content length {ContentLength}", uri, crawledPage.Content.Text.Length);
        }

        var crawledWebPage = CreateCrawledWebPage(crawledPage);

        if (string.IsNullOrWhiteSpace(crawledWebPage.Content))
        {
            logger.LogInformation("Page {Uri} had no content after processing. Skipping...", uri);
            return;
        }

        processingQueue.Add(crawledWebPage);
    }

    private WebPageContent CreateCrawledWebPage(CrawledPage crawledPage)
    {
        var document = crawledPage.AngleSharpHtmlDocument;
        var title = document.Title;

        // Remove script and style elements
        var excludedElements = document.All
            .Where(n => n is IHtmlScriptElement || n is IHtmlStyleElement)
            .ToList();

        foreach (var element in excludedElements)
        {
            element.Remove();
        }

        var content = TextCleanup.Cleanup(document.Body.TextContent);

        logger.LogInformation("Created CrawledWebPage for {Uri} with title '{Title}' and content length {ContentLength}",
            crawledPage.Uri.AbsoluteUri, title, content.Length);

        return new WebPageContent(crawledPage.Uri, title, content);
    }
}
