using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace FractureGuard.Api.Infrastructure;

public interface IVectorSearchService
{
    Task<IReadOnlyList<string>> SearchAsync(string query, int topK = 3);
}

public class VectorSearchService : IVectorSearchService
{
    private readonly SearchClient _client;

    public VectorSearchService(IConfiguration config)
    {
        _client = new SearchClient(
            new Uri(config["AZURE_SEARCH_ENDPOINT"] ?? "http://localhost:9200"),
            config["AZURE_SEARCH_INDEX"] ?? "safety-manuals",
            new AzureKeyCredential(config["AZURE_SEARCH_KEY"] ?? "dev-key")
        );
    }

    public async Task<IReadOnlyList<string>> SearchAsync(string query, int topK = 3)
    {
        var options = new SearchOptions { Size = topK, Select = { "content" } };
        var results = await _client.SearchAsync<SearchDocument>(query, options);
        var chunks = new List<string>();
        await foreach (var result in results.Value.GetResultsAsync())
            if (result.Document.TryGetValue("content", out var content))
                chunks.Add(content?.ToString() ?? string.Empty);
        return chunks;
    }
}
