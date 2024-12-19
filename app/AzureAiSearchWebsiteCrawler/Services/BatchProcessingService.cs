using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace AzureAiSearchWebsiteCrawler.Services;

public class BatchProcessingService(BlockingCollection<WebPageContent> processingQueue,
    AzureAiSearchService azureAiSearchService,
    ILogger<BatchProcessingService> logger,
    IOptions<WebCrawlerOptions> options) : BackgroundService
{
    private readonly int _batchSize = options.Value.MaxBatchSize;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Batch processing service started.");
        logger.LogInformation("Batch size: {BatchSize}", _batchSize);

        var batch = new List<WebPageContent>();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (batch.Count < _batchSize && processingQueue.TryTake(out var webPageContent, Timeout.Infinite, stoppingToken))
                {
                    batch.Add(webPageContent);
                }

                if (batch.Count > 0)
                {
                    logger.LogInformation("Processing batch of {BatchSize} pages.", batch.Count);
                    await azureAiSearchService.IndexPagesAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Batch processing service cancellation requested.");
        }
        finally
        {
            if (batch.Count > 0)
            {
                logger.LogInformation("Processing remaining batch of {BatchSize} pages before shutdown.", batch.Count);
                await azureAiSearchService.IndexPagesAsync(batch);
            }

            logger.LogInformation("Batch processing service stopped.");
        }
    }
}
