using System.ComponentModel.DataAnnotations;

namespace AzureAiSearchWebsiteCrawler.Configs;

public class WebCrawlerOptions : ConfigBase
{
    [Required]
    public string Url { get; set; }

    [Required]
    public int MaxPagesToCrawl { get; set; } = 300;

    [Required]
    public int MaxCrawlDepth { get; set; } = 5;

    [Required]
    public int MaxRetryAttempts { get; set; } = 3;

    [Required]
    public int MaxBatchSize { get; set; } = 100;

    public int ProcessingTimeoutMinutes { get; set; } = 60;
}
