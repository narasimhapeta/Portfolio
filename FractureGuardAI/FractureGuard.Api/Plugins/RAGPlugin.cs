using System.ComponentModel;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Infrastructure;

namespace FractureGuard.Api.Plugins;

public class RAGPlugin(IVectorSearchService searchService)
{
    [KernelFunction, Description("Searches safety manuals and protocols relevant to the operator's question")]
    public async Task<string> GetSafetyContextAsync(
        [Description("The operator's question or risk scenario")] string query)
    {
        var chunks = await searchService.SearchAsync(query, topK: 3);
        return chunks.Count == 0
            ? "No relevant safety protocols found."
            : string.Join("\n---\n", chunks);
    }
}
