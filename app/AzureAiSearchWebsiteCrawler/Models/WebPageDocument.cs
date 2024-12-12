using System.Text.Json.Serialization;

namespace AzureAiSearchWebsiteCrawler.Models;


public class WebPageDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("content_vector")]
    public ReadOnlyMemory<float> ContentVector { get; set; }

    [JsonPropertyName("chunk_number")]
    public string ChunkNumber { get; set; }
}
