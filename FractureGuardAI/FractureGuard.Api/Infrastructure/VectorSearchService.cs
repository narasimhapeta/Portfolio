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
        var endpoint = config["AZURE_SEARCH_ENDPOINT"] ?? "http://localhost:9200";
        var key = config["AZURE_SEARCH_KEY"] ?? "dev-key";
        var index = config["AZURE_SEARCH_INDEX"] ?? "safety-manuals";

        _client = new SearchClient(
            new Uri(endpoint),
            index,
            new AzureKeyCredential(key)
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
