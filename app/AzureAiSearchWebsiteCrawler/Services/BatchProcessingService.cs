using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureAiSearchWebsiteCrawler.Services;

public class BatchProcessingService(ILogger<BatchProcessingService> logger,
    AzureAiSearchService azureAiSearchService,
    IOptions<WebCrawlerOptions> options,
    ItemQueue<WebPageContent> webPageContentQueue) : BackgroundService
{
    private readonly int _maxBatchSize = options.Value.MaxBatchSize;
    public static bool IsProcessingCompleted { get; internal set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Batch processing service started. Max batch size: {BatchSize}", _maxBatchSize);

        while (!webPageContentQueue.IsCompleted)
        {
            var webPageContentBatch = new List<WebPageContent>();
            while (webPageContentBatch.Count < _maxBatchSize && webPageContentQueue.TryDequeue(out var webPageContent))
            {
                logger.LogDebug("Dequeued item for batch. Current batch size: {CurrentBatchSize}", webPageContentBatch.Count + 1);

                webPageContentBatch.Add(webPageContent);
            }

            if (webPageContentBatch.Count > 0)
            {

                await ProcessBatchAsync(webPageContentBatch, stoppingToken);
            }
            else
            {
                logger.LogDebug("No items available for batching. Waiting for new items...");
                await Task.Delay(200, stoppingToken); // Wait for new items
            }
        }

        logger.LogInformation("Batch processing service completed.");

        IsProcessingCompleted = true;

        logger.LogInformation("All items have been processed. Stopping the application.");
    }

    private async Task ProcessBatchAsync(List<WebPageContent> batch, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing batch of {BatchSize} items", batch.Count);
        await azureAiSearchService.IndexPagesAsync(batch, cancellationToken);
        logger.LogInformation("Batch processing of {BatchSize} items completed", batch.Count);
    }
}
