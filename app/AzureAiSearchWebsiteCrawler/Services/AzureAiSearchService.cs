using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using AzureAiSearchWebsiteCrawler.Utilities.Chunking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;

namespace AzureAiSearchWebsiteCrawler.Services;

public class AzureAiSearchService
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly SearchClient _searchClient;
    private readonly ILogger<AzureAiSearchService> _logger;
    private readonly IOptions<AzureOpenAiOptions> _openAiOptions;
    private readonly AzureOpenAiService _azureOpenAiService;
    private readonly ITextSplitter _textSplitter;
    private readonly string _indexName;

    public AzureAiSearchService(ILogger<AzureAiSearchService> logger,
        IOptions<AzureAiSearchOptions> searchOptions,
        IOptions<WebCrawlerOptions> crawlerOptions,
        IOptions<AzureOpenAiOptions> openAiOptions,
        AzureOpenAiService azureOpenAiService,
        ITextSplitter textSplitter)
    {
        _logger = logger;
        _openAiOptions = openAiOptions;
        _azureOpenAiService = azureOpenAiService;
        _textSplitter = textSplitter;
        var endpoint = searchOptions.Value.EndpointUrl;
        _indexName = searchOptions.Value.IndexName;

        if (string.IsNullOrEmpty(_indexName))
        {
            var crawlerUrl = new Uri(crawlerOptions.Value.Url);
            _indexName = $"{crawlerUrl.Host.Replace(".", "-")}-index";
        }

        if (string.IsNullOrWhiteSpace(searchOptions.Value.ApiKey))
        {
            logger.LogInformation("Azure Search API Key is not provided. Using DefaultAzureCredential.");

            var credential = new DefaultAzureCredential();

            _searchIndexClient = new(endpoint, credential);
        }
        else
        {
            logger.LogInformation("Azure Search API Key is provided. Using AzureKeyCredential.");
            _searchIndexClient = new(endpoint, new AzureKeyCredential(searchOptions.Value.ApiKey));
        }

        _searchClient = _searchIndexClient.GetSearchClient(_indexName);

        _logger.LogInformation("AzureAiSearchService initialized with index name: {IndexName}", _indexName);
    }

    public async Task CreateIndexAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting CreateIndexAsync method");

        var existingIndexes = new List<string>();
        await foreach (var index in _searchIndexClient.GetIndexNamesAsync())
        {
            existingIndexes.Add(index);
        }

        if (existingIndexes.Contains(_indexName))
        {
            _logger.LogWarning("Index '{IndexName}' already exists. Skipping creation.", _indexName);
            return;
        }
        else
        {
            _logger.LogInformation("Creating index '{IndexName}'", _indexName);
        }

        const string prefix = "wsi"; // Web Search Index
        var vectorSearchProfileName = $"{prefix}-vector-profile";
        var vectorSearchHnswConfig = $"{prefix}-hnsw-vector-config";
        var vectorSearchExhaustiveKnnConfig = $"{prefix}-eknn-vector-config";
        var semanticConfigurationName = $"{prefix}-semantic-config";
        var vectorizerName = $"{_indexName}-vectorizer";

        var idField = new SimpleField("id", SearchFieldDataType.String)
        {
            IsKey = true,
            IsFilterable = true
        };

        var urlField = new SearchableField("url")
        {
            IsSortable = true,
            IsFilterable = true
        };

        var titleField = new SearchableField("title")
        {
            IsSortable = true,
            IsFilterable = true
        };

        var contentField = new SearchableField("content");

        var contentVectorField = new VectorSearchField("content_vector", _openAiOptions.Value.EmbeddingModelDimensions, vectorSearchProfileName);

        var chunkNumberField = new SearchableField("chunk_number")
        {
            IsFilterable = true
        };

        var searchIndexDefinition = new SearchIndex(_indexName)
        {
            Fields = { idField, urlField, titleField, contentField, contentVectorField, chunkNumberField },
            VectorSearch = new()
            {
                Profiles =
                {
                    new VectorSearchProfile(vectorSearchProfileName, vectorSearchHnswConfig)
                    {
                        VectorizerName = vectorizerName
                    }
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(vectorSearchHnswConfig),
                    new ExhaustiveKnnAlgorithmConfiguration(vectorSearchExhaustiveKnnConfig)
                },
                Vectorizers =
                {
                    new AzureOpenAIVectorizer(vectorizerName)
                    {
                        Parameters = new AzureOpenAIVectorizerParameters()
                        {
                            ResourceUri = _openAiOptions.Value.EndpointUrl,
                            DeploymentName = _openAiOptions.Value.EmbeddingModelDeployment,
                            ModelName = new AzureOpenAIModelName(_openAiOptions.Value.EmbeddingModelDeployment.ToLower())
                        }
                    }
                }
            },
            SemanticSearch = new()
            {
                DefaultConfigurationName = semanticConfigurationName,
                Configurations =
                {
                    new SemanticConfiguration(semanticConfigurationName, new()
                    {
                        TitleField = new SemanticField(titleField.Name),
                        ContentFields =
                        {
                            new SemanticField(contentField.Name)
                        }
                    })
                }
            }
        };

        var searchIndex = await _searchIndexClient.CreateOrUpdateIndexAsync(searchIndexDefinition, cancellationToken: cancellationToken);

        _logger.LogInformation("Index '{IndexName}' created successfully", searchIndex.Value.Name);
    }

    public async Task IndexPagesAsync(IList<WebPageContent> crawledWebPages, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting IndexPagesAsync method");

        var documents = new List<WebPageSearchDocument>();

        foreach (var crawledWebPage in crawledWebPages)
        {
            var textChunks = _textSplitter.SplitTextPages([new TextPage(0, 0, crawledWebPage.Content)]).ToList();
            var textChunksContent = textChunks.Select(x => x.Text).ToList();

            _logger.LogInformation("Generating embeddings for {ChunkCount} chunks for page {Url}", textChunks.Count, crawledWebPage.Url);
            var embeddings = await _azureOpenAiService.GenerateEmbeddingsAsync(textChunksContent, cancellationToken);

            for (int chunkIndex = 0; chunkIndex < textChunks.Count; chunkIndex++)
            {
                var textChunk = textChunks[chunkIndex];
                var chunkNumber = $"{(chunkIndex + 1):00}_{textChunks.Count:00}";
                var contentHash = crawledWebPage.Content.ComputeSha256Hash();
                var chunkHash = textChunk.Text.ComputeSha256Hash();
                var id = $"{contentHash}_{chunkHash}";
                _logger.LogDebug("Preparing chunk {ChunkNumber} for page {Url} (ID: {Id})", chunkNumber, crawledWebPage.Url, id);

                var document = new WebPageSearchDocument
                {
                    Id = id,
                    Url = crawledWebPage.Url.ToString(),
                    Title = crawledWebPage.Title,
                    Content = textChunk.Text,
                    ContentVector = embeddings[chunkIndex],
                    ChunkNumber = chunkNumber
                };

                documents.Add(document);
            }
        }

        _logger.LogInformation("Total documents to index: {DocumentCount}", documents.Count);
        // For a large update, batching (up to 1000 documents per batch, or about 16 MB per batch) is recommended and will significantly improve indexing performance.
        // https://learn.microsoft.com/en-us/rest/api/searchservice/addupdate-or-delete-documents#document-actions

        int batchSize = 1000;
        int batchNumber = 0;
        int totalIndexed = 0;

        foreach (var batch in documents.Chunk(batchSize))
        {
            batchNumber++;
            _logger.LogInformation("Indexing batch {BatchNumber} with {BatchSize} documents...", batchNumber, batch.Count());

            var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.MergeOrUpload(batch), cancellationToken: cancellationToken);
                batchStopwatch.Stop();

                int succeeded = result.Value.Results.Count(r => r.Succeeded);
                int failed = result.Value.Results.Count(r => !r.Succeeded);

                totalIndexed += succeeded;

                _logger.LogInformation(
                    "Batch {BatchNumber} indexed in {ElapsedMs} ms. Succeeded: {Succeeded}, Failed: {Failed}",
                    batchNumber, batchStopwatch.ElapsedMilliseconds, succeeded, failed);

                if (failed > 0)
                {
                    foreach (var r in result.Value.Results.Where(r => !r.Succeeded))
                    {
                        _logger.LogWarning("Failed to index document ID: {Id}, Error: {ErrorMessage}", r.Key, r.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                batchStopwatch.Stop();
                _logger.LogError(ex, "Exception while indexing batch {BatchNumber} after {ElapsedMs} ms", batchNumber, batchStopwatch.ElapsedMilliseconds);
            }
        }

        _logger.LogInformation("Indexing complete. Total successfully indexed documents: {TotalIndexed}", totalIndexed);
    }
}
