using Azure.AI.OpenAI;
using Azure.Identity;
using AzureAiSearchWebsiteCrawler.Configs;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using Serilog.Core;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureAiSearchWebsiteCrawler.Services;

public class AzureOpenAiService
{
    private readonly ILogger<AzureOpenAiService> _logger;
    private readonly IOptions<AzureOpenAiOptions> _azureOpenAiOptions;
    private readonly AzureOpenAIClient _azureOpenAIClient;

    // embedding generation is limited to 16 items at a time
    private readonly int _batchSize = 16;

    public AzureOpenAiService(ILogger<AzureOpenAiService> logger, IOptions<AzureOpenAiOptions> options)
    {
        _logger = logger;
        _azureOpenAiOptions = options;

        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            logger.LogInformation("Azure OpenAI API Key is not provided. Using DefaultAzureCredential.");
            _azureOpenAIClient = new(options.Value.EndpointUrl, new DefaultAzureCredential());
        }
        else
        {
            logger.LogInformation("Azure OpenAI API Key is provided. Using ApiKeyCredential.");
            _azureOpenAIClient = new(options.Value.EndpointUrl, new ApiKeyCredential(options.Value.ApiKey));
        }
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string content)
    {
        _logger.LogDebug("Starting GenerateEmbeddingAsync with content length: {ContentLength}", content.Length);

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Content is null or whitespace.");
            throw new ArgumentException($"'{nameof(content)}' cannot be null or whitespace.", nameof(content));
        }

        content = content.Trim();
        _logger.LogDebug("Trimmed content: {Content}", content);

        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_azureOpenAiOptions.Value.EmbeddingModelDeployment);
        _logger.LogDebug("Retrieved embedding client for deployment: {Deployment}", _azureOpenAiOptions.Value.EmbeddingModelDeployment);

        EmbeddingGenerationOptions embeddingOptions = new()
        {
            Dimensions = _azureOpenAiOptions.Value.EmbeddingModelDimensions
        };
        _logger.LogDebug("Created embedding options with dimensions: {Dimensions}", _azureOpenAiOptions.Value.EmbeddingModelDimensions);

        OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(content, embeddingOptions);
        _logger.LogDebug("Generated embedding for content.");

        return embedding.ToFloats();
    }

    public async Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> listOfContent)
    {
        _logger.LogDebug("Starting GenerateEmbeddingAsync with list of content. Count: {Count}", listOfContent.Count);

        if (listOfContent == null || listOfContent.Count == 0)
        {
            _logger.LogWarning("List of content is null or empty.");
            throw new ArgumentException($"'{nameof(listOfContent)}' cannot be null or empty.", nameof(listOfContent));
        }

        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_azureOpenAiOptions.Value.EmbeddingModelDeployment);
        _logger.LogDebug("Retrieved embedding client for deployment: {Deployment}", _azureOpenAiOptions.Value.EmbeddingModelDeployment);

        EmbeddingGenerationOptions embeddingOptions = new()
        {
            Dimensions = _azureOpenAiOptions.Value.EmbeddingModelDimensions
        };
        _logger.LogDebug("Created embedding options with dimensions: {Dimensions}", _azureOpenAiOptions.Value.EmbeddingModelDimensions);

        _logger.LogDebug("Generating embeddings for list of content in batches of {BatchSize}.", _batchSize);
        var results = new List<ReadOnlyMemory<float>>();

        for (int i = 0; i < listOfContent.Count; i += _batchSize)
        {
            var batch = listOfContent.Skip(i).Take(_batchSize).ToList();
            _logger.LogDebug("Processing batch {BatchNumber} with {BatchSize} items.", i / _batchSize + 1, batch.Count);

            var embeddings = await embeddingClient.GenerateEmbeddingsAsync(batch, embeddingOptions);
            _logger.LogDebug("Generated embeddings for batch {BatchNumber}.", i / _batchSize + 1);

            results.AddRange(embeddings.Value.Select(e => e.ToFloats()));
        }

        _logger.LogDebug("Generated embeddings for all batches. Total count: {Count}", results.Count);
        return results;

    }
}
