using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AzureAiSearchWebsiteCrawler.Services;
public class WebCrawlerService(ILogger<WebCrawlerService> logger,
    IOptions<WebCrawlerOptions> options,
    AzureAiSearchService azureAiSearchService)
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
    private static readonly ConcurrentDictionary<string, CrawledWebPage> crawledPages = new();
    private static readonly SemaphoreSlim semaphore = new(1, 1);

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

        if (!crawledPages.TryAdd(uri, crawledWebPage))
        {
            logger.LogInformation("Page {Uri} has already been processed.", uri);
            return;
        }

        logger.LogInformation("Page {Uri} added to crawled pages collection.", uri);

        if (crawledPages.Count >= 10)
        {
            logger.LogInformation("Crawled pages count reached threshold, starting batch indexing.");
            semaphore.Wait();
            try
            {
                if (crawledPages.Count >= 10)
                {
                    IndexCrawledPagesBatchAsync().GetAwaiter().GetResult();
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    private async Task IndexCrawledPagesBatchAsync()
    {
        var pagesToIndex = new List<CrawledWebPage>();

        foreach (var kvp in crawledPages)
        {
            if (pagesToIndex.Count >= 10)
            {
                break;
            }

            pagesToIndex.Add(kvp.Value);
            crawledPages.TryRemove(kvp.Key, out _);
        }

        logger.LogInformation("Indexing batch of {BatchSize} pages.", pagesToIndex.Count);
        await azureAiSearchService.IndexPagesAsync(pagesToIndex);
    }

    private CrawledWebPage CreateCrawledWebPage(CrawledPage crawledPage)
    {
        var document = crawledPage.AngleSharpHtmlDocument;
        var title = document.Title;

        var excludedElementTypes = new List<Type>
        {
            typeof(IHtmlScriptElement),
            typeof(IHtmlStyleElement)
        };

        var excludedElements = document.Body.Descendents()
            .Where(n => excludedElementTypes.Contains(n.GetType()))
            .Cast<IHtmlElement>()
            .ToList();

        foreach (var element in excludedElements)
        {
            element.Remove();
        }

        var content = document.Body.TextContent;

        logger.LogInformation("Created CrawledWebPage for {Uri} with title '{Title}' and content length {ContentLength}", crawledPage.Uri.AbsoluteUri, title, content.Length);

        return new CrawledWebPage(crawledPage.Uri, title, content);
    }
}
