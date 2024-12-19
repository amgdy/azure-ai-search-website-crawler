using System.ComponentModel.DataAnnotations;

namespace AzureAiSearchWebsiteCrawler.Configs;

public class TextSplitterOptions : ConfigBase
{
    [Required]
    public int MaxTokensPerSection { get; set; } = 500;

    [Required]
    public int DefaultOverlapPercent { get; set; } = 10;

    [Required]
    public int DefaultSectionLength { get; set; } = 1000;

    [Required]
    public int SentenceSearchLimit { get; set; } = 100;
}
