using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace AutoInsuranceMind.API.Services;

public class AzureSearchService
{
    private readonly SearchClient? _searchClient;
    private readonly SearchIndexClient? _indexClient;
    private readonly string _indexName;
    private readonly ILogger<AzureSearchService> _logger;
    public readonly bool IsConfigured;

    public AzureSearchService(ILogger<AzureSearchService> logger, IConfiguration config, EmbeddingService embeddingService)
    {
        _logger = logger;
        _indexName = config["AzureCognitiveSearch:IndexName"] ?? "policy-documents";

        var endpoint = config["AzureCognitiveSearch:Endpoint"] ?? string.Empty;
        var adminKey = config["AzureCognitiveSearch:AdminKey"] ?? string.Empty;

        bool isPlaceholder(string s) => string.IsNullOrWhiteSpace(s) || s.StartsWith("YOUR_");
        IsConfigured = !isPlaceholder(endpoint) && !isPlaceholder(adminKey) && embeddingService.IsConfigured;

        if (IsConfigured)
        {
            var credential = new AzureKeyCredential(adminKey);
            _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
            _searchClient = new SearchClient(new Uri(endpoint), _indexName, credential);
            _logger.LogInformation("Azure Cognitive Search configured. Index: {Index}", _indexName);
        }
    }

    // Required field names our schema defines — used to detect stale/wrong indexes
    private static readonly HashSet<string> RequiredFields = new() { "id", "content", "contentVector", "documentId", "customerId" };

    public async Task EnsureIndexExistsAsync(int vectorDimensions = 1536)
    {
        if (!IsConfigured) return;

        try
        {
            var existing = await _indexClient!.GetIndexAsync(_indexName);
            var existingFields = existing.Value.Fields.Select(f => f.Name).ToHashSet();

            if (!RequiredFields.IsSubsetOf(existingFields))
            {
                _logger.LogWarning(
                    "Index '{Index}' exists but is missing required fields ({Missing}). Deleting and recreating.",
                    _indexName,
                    string.Join(", ", RequiredFields.Except(existingFields)));
                await _indexClient.DeleteIndexAsync(_indexName, CancellationToken.None);
                await CreateIndexAsync(vectorDimensions);
            }
            else
            {
                _logger.LogInformation("Search index '{Index}' exists with correct schema.", _indexName);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            await CreateIndexAsync(vectorDimensions);
        }
    }

    private async Task CreateIndexAsync(int vectorDimensions)
    {
        _logger.LogInformation("Creating search index '{Index}' with {Dims}-dim vectors...", _indexName, vectorDimensions);
        var index = new SearchIndex(_indexName)
        {
            Fields =
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SearchableField("content") { IsFilterable = false },
                new SimpleField("fileName", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("customerId", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("chunkIndex", SearchFieldDataType.Int32),
                new VectorSearchField("contentVector", vectorDimensions, "policy-vector-profile"),
            },
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration("policy-hnsw") },
                Profiles = { new VectorSearchProfile("policy-vector-profile", "policy-hnsw") }
            }
        };
        await _indexClient!.CreateIndexAsync(index);
        _logger.LogInformation("Search index '{Index}' created successfully.", _indexName);
    }

    public async Task IndexChunksAsync(string documentId, string fileName, string customerId,
        List<(string Text, float[] Vector, int Index)> chunks)
    {
        if (!IsConfigured) return;

        var batch = chunks.Select(c => new SearchDocument
        {
            ["id"] = $"{documentId}-chunk-{c.Index}",
            ["content"] = c.Text,
            ["fileName"] = fileName,
            ["documentId"] = documentId,
            ["customerId"] = customerId,
            ["chunkIndex"] = c.Index,
            ["contentVector"] = c.Vector,
        }).ToList();

        try
        {
            await _searchClient!.IndexDocumentsAsync(IndexDocumentsBatch.Upload(batch));
            _logger.LogInformation("Indexed {Count} chunks for document {DocId}", chunks.Count, documentId);
        }
        catch (RequestFailedException ex) when (ex.Status == 400 && ex.Message.Contains("does not exist"))
        {
            // Index schema mismatch — delete, recreate, and retry once
            _logger.LogWarning("Schema mismatch on indexing. Recreating index and retrying. Error: {Error}", ex.Message);
            await _indexClient!.DeleteIndexAsync(_indexName, CancellationToken.None);
            await CreateIndexAsync(chunks.First().Vector.Length);
            await _searchClient!.IndexDocumentsAsync(IndexDocumentsBatch.Upload(batch));
            _logger.LogInformation("Retry succeeded. Indexed {Count} chunks for {DocId}", chunks.Count, documentId);
        }
    }

    public async Task<List<(string Content, string FileName, double Score)>> SearchAsync(
        float[] queryVector, string customerId, int topK = 10)
    {
        if (!IsConfigured) return new();

        var options = new SearchOptions
        {
            Filter = $"customerId eq '{customerId}'",
            Size = topK,
        };
        options.VectorSearch = new VectorSearchOptions
        {
            Queries = { new VectorizedQuery(queryVector) { KNearestNeighborsCount = topK, Fields = { "contentVector" } } }
        };

        var response = await _searchClient!.SearchAsync<SearchDocument>("*", options);
        var results = new List<(string, string, double)>();

        await foreach (var r in response.Value.GetResultsAsync())
        {
            var content = r.Document["content"]?.ToString() ?? string.Empty;
            var fileName = r.Document["fileName"]?.ToString() ?? string.Empty;
            results.Add((content, fileName, r.Score ?? 0));
        }

        return results;
    }

    public async Task DeleteDocumentChunksAsync(string documentId)
    {
        if (!IsConfigured) return;

        // Search for all chunks belonging to this document then delete them
        var options = new SearchOptions { Filter = $"documentId eq '{documentId}'", Size = 100 };
        var response = await _searchClient!.SearchAsync<SearchDocument>("*", options);
        var ids = new List<string>();
        await foreach (var r in response.Value.GetResultsAsync())
            ids.Add(r.Document["id"]?.ToString() ?? string.Empty);

        if (ids.Count > 0)
        {
            var batch = IndexDocumentsBatch.Delete("id", ids);
            await _searchClient.IndexDocumentsAsync(batch);
            _logger.LogInformation("Deleted {Count} chunks for document {DocId}", ids.Count, documentId);
        }
    }
}
