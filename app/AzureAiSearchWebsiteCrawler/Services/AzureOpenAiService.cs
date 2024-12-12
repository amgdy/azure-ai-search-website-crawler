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
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException($"'{nameof(content)}' cannot be null or whitespace.", nameof(content));
        }

        content = content.Trim();

        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_azureOpenAiOptions.Value.EmbeddingModelDeployment);

        EmbeddingGenerationOptions embeddingOptions = new()
        {
            Dimensions = _azureOpenAiOptions.Value.EmbeddingModelDimensions
        };

        OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(content, embeddingOptions);
        return embedding.ToFloats();
    }
}
