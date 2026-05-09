#pragma warning disable SKEXP0001, SKEXP0010
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace AutoInsuranceMind.API.Services;

public class EmbeddingService
{
    private readonly ITextEmbeddingGenerationService? _embeddingService;
    private readonly ILogger<EmbeddingService> _logger;
    public readonly bool IsConfigured;
    public readonly int Dimensions;

    public EmbeddingService(ILogger<EmbeddingService> logger, IConfiguration config)
    {
        _logger = logger;

        var endpoint = config["AzureOpenAI:Endpoint"] ?? string.Empty;
        var apiKey = config["AzureOpenAI:ApiKey"] ?? string.Empty;
        var deployment = config["AzureOpenAI:EmbeddingDeploymentName"] ?? string.Empty;
        Dimensions = int.TryParse(config["AzureOpenAI:EmbeddingDimensions"], out var d) ? d : 1536;

        bool isPlaceholder(string s) => string.IsNullOrWhiteSpace(s) || s.StartsWith("YOUR_");
        IsConfigured = !isPlaceholder(endpoint) && !isPlaceholder(apiKey) && !isPlaceholder(deployment);

        if (IsConfigured)
        {
            try
            {
                var cleanEndpoint = new Uri(endpoint).GetLeftPart(UriPartial.Authority) + "/";
                var builder = Kernel.CreateBuilder();
                builder.AddAzureOpenAITextEmbeddingGeneration(deployment, cleanEndpoint, apiKey);
                var kernel = builder.Build();
                _embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                _logger.LogInformation("Embedding service ready. Deployment: {Deployment}, Dimensions: {Dims}", deployment, Dimensions);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to init embedding service: {Error}", ex.Message);
                IsConfigured = false;
            }
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var result = await _embeddingService!.GenerateEmbeddingAsync(text);
        return result.ToArray();
    }

    /// <summary>
    /// Splits text into overlapping chunks suitable for embedding.
    /// </summary>
    public List<string> ChunkText(string text, int chunkSize = 800, int overlap = 100)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        // Split on sentence boundaries where possible
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var start = 0;

        while (start < words.Length)
        {
            var end = Math.Min(start + chunkSize, words.Length);
            chunks.Add(string.Join(" ", words[start..end]));
            start += chunkSize - overlap;
            if (start >= words.Length) break;
        }

        return chunks;
    }
}
