using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AzureAiSearchWebsiteCrawler.Services;

internal class ApplicationStartupService(ILogger<ApplicationStartupService> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    WebCrawlerService webCrawlerService,
    AzureAiSearchService azureAiSearchService) : BackgroundService
{
    public const string ActivitySourceName = nameof(ApplicationStartupService);
    private static readonly ActivitySource _activitySource = new(ActivitySourceName);


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("Start crawling the website", ActivityKind.Internal);

        logger.LogInformation("Received a request to start the crawl and index process.");

        try
        {
            logger.LogInformation("Initiating the index creation process.");
            await azureAiSearchService.CreateIndexAsync();
            logger.LogInformation("Index creation completed successfully.");

            logger.LogInformation("Initiating the web crawling process.");
            await webCrawlerService.StartCrawlAsync();
            logger.LogInformation("Web crawling completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during the crawl and index process.");
            activity?.AddException(ex);
            return;
        }

        logger.LogInformation("Crawl and index process completed successfully.");
        hostApplicationLifetime.StopApplication();
    }
}
