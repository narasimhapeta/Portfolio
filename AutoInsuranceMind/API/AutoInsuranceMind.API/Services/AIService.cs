using AutoInsuranceMind.API.Data;
using AutoInsuranceMind.API.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AutoInsuranceMind.API.Services;

public class AIService
{
    private readonly ILogger<AIService> _logger;
    private readonly EmbeddingService? _embeddingService;
    private readonly AzureSearchService? _searchService;
    private Kernel? _kernel;
    private IChatCompletionService? _chatCompletion;
    private readonly bool _isConfigured;

    public AIService(ILogger<AIService> logger, IConfiguration config,
        EmbeddingService embeddingService, AzureSearchService searchService)
    {
        _logger = logger;
        _embeddingService = embeddingService;
        _searchService = searchService;

        var apiKey = config["OpenAI:ApiKey"] ?? string.Empty;
        var azureEndpoint = config["AzureOpenAI:Endpoint"] ?? string.Empty;
        var azureApiKey = config["AzureOpenAI:ApiKey"] ?? string.Empty;
        var azureDeployment = config["AzureOpenAI:DeploymentName"] ?? string.Empty;

        bool isPlaceholder(string s) => string.IsNullOrWhiteSpace(s) || s.StartsWith("YOUR_") || s.StartsWith("PLACEHOLDER");

        var useAzure = !isPlaceholder(azureEndpoint) && !isPlaceholder(azureApiKey) && !isPlaceholder(azureDeployment);
        var useOpenAI = !useAzure && !isPlaceholder(apiKey);
        _isConfigured = useAzure || useOpenAI;

        _logger.LogInformation("AIService init — useAzure={UseAzure} useOpenAI={UseOpenAI} endpoint={Endpoint}",
            useAzure, useOpenAI, azureEndpoint.Length > 0 ? azureEndpoint[..Math.Min(60, azureEndpoint.Length)] + "…" : "(empty)");

        if (_isConfigured)
        {
            try
            {
                var builder = Kernel.CreateBuilder();
                if (useAzure)
                {
                    // Endpoint must be the base resource URL only, e.g. https://YOUR-RESOURCE.cognitiveservices.azure.com/
                    // Strip any path/query if accidentally included
                    var cleanEndpoint = new Uri(azureEndpoint).GetLeftPart(UriPartial.Authority) + "/";
                    builder.AddAzureOpenAIChatCompletion(azureDeployment, cleanEndpoint, azureApiKey);
                    _logger.LogInformation("Semantic Kernel initialised with Azure OpenAI. Endpoint: {Endpoint} | Deployment: {Deployment}", cleanEndpoint, azureDeployment);
                }
                else
                {
                    builder.AddOpenAIChatCompletion(config["OpenAI:Model"] ?? "gpt-4o-mini", apiKey);
                    _logger.LogInformation("Semantic Kernel initialised with OpenAI.");
                }
                _kernel = builder.Build();
                _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Semantic Kernel ready.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialise Semantic Kernel: {Error}. Falling back to mock mode.", ex.Message);
                _isConfigured = false;
            }
        }
        else
        {
            _logger.LogWarning("No AI API key configured — running in mock RAG mode.");
        }
    }

    public async Task<(string Response, List<string> Sources, bool UsedRag)> ProcessMessageAsync(string userMessage, string? documentId = null)
    {
        // Step 1: Retrieve relevant chunks — vector search (Azure) or keyword search (local)
        var (ragContext, sources) = await RetrieveRelevantDocumentsAsync(userMessage, documentId);
        var usedRag = sources.Count > 0;

        if (_isConfigured && _chatCompletion != null)
            return await CallOpenAiAsync(userMessage, ragContext, sources, usedRag);

        // Fallback: intelligent mock response using mock policy data
        return (BuildMockResponse(userMessage, ragContext, sources), sources, usedRag);
    }

    private string BuildPolicyMetaContext()
    {
        // Non-financial portal metadata only — policy number, status, dates.
        // All financial figures (premium, limits, deductibles) are intentionally excluded
        // so the AI reads them exclusively from the uploaded document.
        var policies = MockDataStore.Policies.Where(p => p.CustomerId == "cust-001").ToList();
        if (!policies.Any()) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== PORTAL POLICY METADATA ===");
        foreach (var p in policies)
        {
            sb.AppendLine($"Policy: {p.PolicyNumber} | Type: {p.Type} | Status: {p.Status}");
            sb.AppendLine($"  Period: {p.StartDate:yyyy-MM-dd} to {p.EndDate:yyyy-MM-dd}");
        }
        sb.AppendLine("Note: For premium amounts, coverage limits, deductibles and all financial details, refer exclusively to the uploaded policy document.");
        return sb.ToString();
    }

    private string BuildFullPolicyContext()
    {
        // Full context including coverages — used when no document is uploaded.
        var policies = MockDataStore.Policies.Where(p => p.CustomerId == "cust-001").ToList();
        if (!policies.Any()) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== CUSTOMER POLICY DATA ===");
        foreach (var p in policies)
        {
            sb.AppendLine($"Policy: {p.PolicyNumber} | Type: {p.Type} | Status: {p.Status}");
            sb.AppendLine($"  Annual Premium: ${p.Premium:N2}");
            sb.AppendLine($"  Period: {p.StartDate:yyyy-MM-dd} to {p.EndDate:yyyy-MM-dd}");
            foreach (var c in p.Coverages)
                sb.AppendLine($"  Coverage: {c.Type} | Limit: ${c.Limit:N0} | Deductible: ${c.Deductible:N0}");
        }
        return sb.ToString();
    }

    private async Task<(string Response, List<string> Sources, bool UsedRag)> CallOpenAiAsync(
        string userMessage, string ragContext, List<string> sources, bool usedRag)
    {
        try
        {
            string systemPrompt;
            string userPrompt;

            if (usedRag)
            {
                // Document is the authoritative source — mock data is supplementary only
                systemPrompt =
                    "You are a helpful auto insurance assistant. " +
                    "The customer has uploaded their actual policy document. " +
                    "ALWAYS answer coverage questions (limits, deductibles, coverages, insured details) " +
                    "using the document context provided below — it is the authoritative source. " +
                    "Only use the portal summary for information not found in the document (e.g. payment status).\n\n" +
                    BuildPolicyMetaContext();

                userPrompt =
                    $"=== POLICY DOCUMENT (authoritative source) ===\n{ragContext}\n\n" +
                    $"Customer question: {userMessage}";
            }
            else
            {
                // No document uploaded — use full mock policy data
                systemPrompt =
                    "You are a helpful auto insurance assistant for a customer portal. " +
                    "Answer questions clearly and concisely based on the customer's policy data below.\n\n" +
                    BuildFullPolicyContext();

                userPrompt = userMessage;
            }

            var messages = new ChatHistory();
            messages.AddSystemMessage(systemPrompt);
            messages.AddUserMessage(userPrompt);

            var result = await _chatCompletion!.GetChatMessageContentAsync(messages);
            return (result.Content ?? "No response generated.", sources, usedRag);
        }
        catch (Exception ex)
        {
            _logger.LogError("OpenAI call failed: {Error}. Using mock fallback.", ex.Message);
            return (BuildMockResponse(userMessage, ragContext, sources), sources, usedRag);
        }
    }

    private async Task<(string Context, List<string> Sources)> RetrieveRelevantDocumentsAsync(string query, string? documentId)
    {
        // Azure path: vector similarity search via Cognitive Search
        if (_searchService?.IsConfigured == true && _embeddingService?.IsConfigured == true)
        {
            try
            {
                var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);
                var results = await _searchService.SearchAsync(queryVector, "cust-001", topK: 10);

                if (results.Count == 0)
                    return (string.Empty, new List<string>());

                _logger.LogInformation("Vector search returned {Count} chunks from Azure Cognitive Search", results.Count);
                for (var i = 0; i < results.Count; i++)
                    _logger.LogInformation("  Chunk {i}: score={Score:F3} | preview={Preview}",
                        i + 1, results[i].Score,
                        results[i].Content.Length > 120 ? results[i].Content[..120] + "…" : results[i].Content);

                var context = string.Join("\n\n---\n\n", results.Select(r =>
                    $"[Source: {r.FileName} | Score: {r.Score:F2}]\n{r.Content}"));
                var sources = results.Select(r => r.FileName).Distinct().ToList();

                return (context, sources);
            }
            catch (Exception ex)
            {
                _logger.LogError("Vector search failed: {Error}. Falling back to keyword search.", ex.Message);
            }
        }

        // Local fallback: keyword search over in-memory documents
        return KeywordSearch(query, documentId);
    }

    private static (string Context, List<string> Sources) KeywordSearch(string query, string? documentId)
    {
        var documents = MockDataStore.Documents
            .Where(d => !string.IsNullOrEmpty(d.ExtractedText));

        if (!string.IsNullOrEmpty(documentId))
            documents = documents.Where(d => d.Id == documentId);

        var queryWords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matched = new List<(UploadedDocument Doc, int Score)>();

        foreach (var doc in documents)
        {
            var text = doc.ExtractedText.ToLowerInvariant();
            var score = queryWords.Count(w => text.Contains(w));
            if (score > 0)
                matched.Add((doc, score));
        }

        var ranked = matched.OrderByDescending(x => x.Score).Take(3).ToList();
        if (ranked.Count == 0)
            return (string.Empty, new List<string>());

        var context = string.Join("\n\n---\n\n", ranked.Select(r =>
            $"[Source: {r.Doc.FileName}]\n{r.Doc.ExtractedText[..Math.Min(1000, r.Doc.ExtractedText.Length)]}"));
        var sources = ranked.Select(r => r.Doc.FileName).ToList();

        return (context, sources);
    }

    private string BuildMockResponse(string userMessage, string ragContext, List<string> sources)
    {
        var lowerQuery = userMessage.ToLowerInvariant();

        // Policy-aware mock answers
        if (lowerQuery.Contains("coverage") || lowerQuery.Contains("limit") || lowerQuery.Contains("deductible"))
        {
            var policies = MockDataStore.Policies.Where(p => p.Status == "active").ToList();
            if (policies.Any())
            {
                var coverageSummary = string.Join(", ", policies.First().Coverages
                    .Select(c => $"{c.Type} (limit: ${c.Limit:N0}, deductible: ${c.Deductible:N0})"));
                return $"Your active policy coverages are: {coverageSummary}. " +
                       (sources.Any() ? $"This is based on your uploaded document: {sources.First()}." : "");
            }
        }

        if (lowerQuery.Contains("premium") || lowerQuery.Contains("payment") || lowerQuery.Contains("cost"))
        {
            var policy = MockDataStore.Policies.FirstOrDefault(p => p.Status == "active");
            return policy != null
                ? $"Your current annual premium is ${policy.Premium:N2}. Monthly payments would be approximately ${policy.Premium / 12:N2}."
                : "No active policy found. Please contact support for premium information.";
        }

        if (lowerQuery.Contains("expir") || lowerQuery.Contains("renew"))
        {
            var policy = MockDataStore.Policies.FirstOrDefault(p => p.Status == "active");
            if (policy != null)
            {
                var daysLeft = (policy.EndDate - DateTime.UtcNow).Days;
                return $"Your policy {policy.PolicyNumber} expires on {policy.EndDate:MMMM d, yyyy} ({daysLeft} days from now). " +
                       "You can renew it from the dashboard or contact us for assistance.";
            }
        }

        if (lowerQuery.Contains("claim"))
            return "To file a claim, please contact our claims department at 1-800-INSURE or log into the portal and select 'File a Claim'. " +
                   "Have your policy number and incident details ready.";

        // RAG-based response when documents are available
        if (sources.Any())
            return $"Based on your uploaded document ({string.Join(", ", sources)}): " +
                   $"The document mentions details about {userMessage.ToLower()}. " +
                   "For a complete answer, please ensure your OpenAI API key is configured so I can provide a full AI-generated response.";

        return $"I'm running in demo mode (no AI API key configured). " +
               $"You asked: \"{userMessage}\". " +
               "In production mode, I would search your policy documents and provide a detailed answer using RAG. " +
               "Try asking about your coverage limits, premiums, or policy expiration dates.";
    }

    public List<ChatMessage> GetChatHistory() => MockDataStore.ChatHistory;

    public void ResetChat() => MockDataStore.ChatHistory.Clear();
}
