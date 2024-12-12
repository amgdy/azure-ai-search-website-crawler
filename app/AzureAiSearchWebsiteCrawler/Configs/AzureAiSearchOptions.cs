using System.ComponentModel.DataAnnotations;

namespace AzureAiSearchWebsiteCrawler.Configs;

public class AzureAiSearchOptions : ConfigBase
{
    [Required]
    public Uri EndpointUrl { get; set; }

    public string ApiKey { get; set; }

    public string IndexName { get; set; }
}
