using System.ComponentModel.DataAnnotations;

namespace AzureAiSearchWebsiteCrawler.Configs;

public class AzureOpenAiOptions : ConfigBase
{
    [Required]
    public Uri EndpointUrl { get; set; }


    public string ApiKey { get; set; }

    [Required]
    public string EmbeddingModelDeployment { get; set; } = "text-embedding-ada-002";

    [Required]
    public int EmbeddingModelDimensions { get; set; } = 1536;

    [Required]
    public int EmbeddingModelMaxTokens { get; set; } = 8191;
}
