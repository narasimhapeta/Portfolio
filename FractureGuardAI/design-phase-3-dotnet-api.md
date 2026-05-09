# Phase 3 — .NET Core API (Orchestrator)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the ASP.NET Core 9 API that acts as the Semantic Kernel orchestrator — managing Plugins, authenticating engineers via Entra ID, persisting chat history in Cosmos DB, publishing ML jobs to RabbitMQ, consuming ML results, and streaming LLM responses to the Angular dashboard.

**Architecture:** Single ASP.NET Core Web API project. Semantic Kernel holds four Plugins (SensorPlugin, RAGPlugin, PredictionPlugin, ReportPlugin). A `BackgroundService` (`AnalysisResultConsumer`) continuously polls the RabbitMQ `analysis-results` queue. `ChatController` streams LLM responses via Server-Sent Events.

**Tech Stack:** .NET 9 · Semantic Kernel 1.21.x · RabbitMQ.Client 6.8.x · Azure.Cosmos 3.43.x · Azure.Search.Documents 11.6.x · Microsoft.Identity.Web 2.19.x · Moq · FluentAssertions · xUnit

**Depends on:** Phase 0 (RabbitMQ + Cosmos DB Emulator), Phase 2 (Node.js Notifier webhook endpoint)

---

## File Map

```
FractureGuard.Api/
├── FractureGuard.Api.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Controllers/
│   └── ChatController.cs        POST /api/chat (SSE streaming), GET /api/chat/{sessionId}
├── Plugins/
│   ├── SensorPlugin.cs          KernelFunction: fetches live sensor snapshot from Node.js
│   ├── RAGPlugin.cs             KernelFunction: vector search on Azure AI Search
│   ├── PredictionPlugin.cs      KernelFunction: role-gated ML job publisher
│   └── ReportPlugin.cs          KernelFunction: generates plain-English risk report via LLM
├── Services/
│   ├── IAnalysisJobService.cs
│   ├── AnalysisJobService.cs    RabbitMQ publisher for analysis-requests queue
│   ├── AnalysisResultConsumer.cs  BackgroundService consuming analysis-results queue
│   ├── INotifierService.cs
│   └── NotifierService.cs       HTTP client that POSTs completed reports to Node.js /notify
├── Models/
│   ├── ChatMessage.cs
│   ├── ChatSession.cs
│   ├── SensorSnapshot.cs
│   ├── AnalysisRequest.cs
│   ├── AnalysisResult.cs
│   └── RiskReport.cs
└── Infrastructure/
    ├── CosmosDbService.cs       Chat session persistence (partitioned by userId)
    └── VectorSearchService.cs   Azure AI Search / FAISS wrapper

FractureGuard.Api.Tests/
├── FractureGuard.Api.Tests.csproj
├── Plugins/
│   ├── SensorPluginTests.cs
│   ├── PredictionPluginTests.cs
│   └── ReportPluginTests.cs
└── Services/
    └── AnalysisJobServiceTests.cs
```

---

### Task 6: Project setup + models + Cosmos DB service

**Files:**
- Create: `FractureGuard.Api/FractureGuard.Api.csproj` (via dotnet CLI)
- Create: `FractureGuard.Api/Models/` (all model files)
- Create: `FractureGuard.Api/Infrastructure/CosmosDbService.cs`
- Create: `FractureGuard.Api/appsettings.json`
- Create: `FractureGuard.Api/appsettings.Development.json`
- Create: `FractureGuard.Api/Program.cs`
- Create: `FractureGuard.Api.Tests/FractureGuard.Api.Tests.csproj` (via dotnet CLI)

- [ ] **Step 1: Scaffold .NET projects**

```bash
dotnet new webapi -n FractureGuard.Api --use-controllers
dotnet new xunit  -n FractureGuard.Api.Tests
dotnet add FractureGuard.Api.Tests/FractureGuard.Api.Tests.csproj reference \
           FractureGuard.Api/FractureGuard.Api.csproj
```

- [ ] **Step 2: Add NuGet packages to API project**

```bash
cd FractureGuard.Api
dotnet add package Microsoft.SemanticKernel --version 1.21.1
dotnet add package Microsoft.SemanticKernel.Connectors.AzureOpenAI --version 1.21.1
dotnet add package Microsoft.Azure.Cosmos --version 3.43.0
dotnet add package Azure.Search.Documents --version 11.6.0
dotnet add package RabbitMQ.Client --version 6.8.1
dotnet add package Microsoft.Identity.Web --version 2.19.0
```

- [ ] **Step 3: Add NuGet packages to test project**

```bash
cd ../FractureGuard.Api.Tests
dotnet add package Moq --version 4.20.70
dotnet add package FluentAssertions --version 6.12.0
```

- [ ] **Step 4: Create `Models/SensorSnapshot.cs`**

```csharp
namespace FractureGuard.Api.Models;

public record SensorSnapshot(
    double PressurePsi,
    double PressureTrendPct,
    double FlowRateBpm,
    double FlowRateVariance,
    double VibrationG,
    double TemperatureC
);
```

- [ ] **Step 5: Create `Models/ChatMessage.cs`**

```csharp
namespace FractureGuard.Api.Models;

public record ChatMessage(
    string Role,        // "user" | "assistant"
    string Content,
    DateTimeOffset Timestamp
);
```

- [ ] **Step 6: Create `Models/ChatSession.cs`**

```csharp
using Newtonsoft.Json;

namespace FractureGuard.Api.Models;

public class ChatSession
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 7: Create `Models/AnalysisRequest.cs` and `Models/AnalysisResult.cs`**

```csharp
// Models/AnalysisRequest.cs
namespace FractureGuard.Api.Models;

public record AnalysisRequest(string SessionId, SensorSnapshot SensorSnapshot);
```

```csharp
// Models/AnalysisResult.cs
namespace FractureGuard.Api.Models;

public record AnalysisResult(
    string SessionId,
    double RiskPct,
    List<string> ContributingFactors,
    double Confidence
);
```

- [ ] **Step 8: Create `Infrastructure/CosmosDbService.cs`**

```csharp
using Microsoft.Azure.Cosmos;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Infrastructure;

public interface ICosmosDbService
{
    Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string userId);
    Task AppendMessageAsync(string sessionId, ChatMessage message);
    Task<List<ChatSession>> GetSessionsByUserAsync(string userId);
}

public class CosmosDbService : ICosmosDbService
{
    private readonly Container _container;

    public CosmosDbService(IConfiguration config)
    {
        var client = new CosmosClient(
            config["COSMOS_ENDPOINT"],
            config["COSMOS_KEY"]
        );
        var db = client.GetDatabase(config["COSMOS_DB"] ?? "FractureGuardDB");
        _container = db.GetContainer("ChatSessions");
    }

    public async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string userId)
    {
        try
        {
            var response = await _container.ReadItemAsync<ChatSession>(
                sessionId, new PartitionKey(userId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var session = new ChatSession { Id = sessionId, UserId = userId };
            await _container.CreateItemAsync(session, new PartitionKey(userId));
            return session;
        }
    }

    public async Task AppendMessageAsync(string sessionId, ChatMessage message)
    {
        var patch = PatchOperation.Add("/messages/-", message);
        await _container.PatchItemAsync<ChatSession>(
            sessionId, new PartitionKey(sessionId), new[] { patch }
        );
    }

    public async Task<List<ChatSession>> GetSessionsByUserAsync(string userId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.createdAt DESC"
        ).WithParameter("@userId", userId);

        var results = new List<ChatSession>();
        var iterator = _container.GetItemQueryIterator<ChatSession>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }
}
```

- [ ] **Step 9: Create `Infrastructure/VectorSearchService.cs`**

```csharp
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
```

- [ ] **Step 10: Create `appsettings.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 11: Create `appsettings.Development.json`**

```json
{
  "Authentication": {
    "Schemes": {
      "Bearer": {
        "ValidAudiences": ["fractureguard-dev"],
        "ValidIssuer": "fractureguard-dev"
      }
    }
  }
}
```

- [ ] **Step 12: Create `Program.cs`**

```csharp
using Microsoft.SemanticKernel;
using Microsoft.Identity.Web;
using FractureGuard.Api.Infrastructure;
using FractureGuard.Api.Plugins;
using FractureGuard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Auth
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// Infrastructure
builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();
builder.Services.AddSingleton<IVectorSearchService, VectorSearchService>();

// Semantic Kernel
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var kernelBuilder = Kernel.CreateBuilder();

    if (!string.IsNullOrEmpty(config["AZURE_OPENAI_ENDPOINT"]))
    {
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: config["AZURE_OPENAI_DEPLOYMENT"] ?? "gpt-4o",
            endpoint: config["AZURE_OPENAI_ENDPOINT"]!,
            apiKey: config["AZURE_OPENAI_API_KEY"]!
        );
    }
    else
    {
        // Ollama local fallback
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: "llama3",
            endpoint: new Uri(config["OLLAMA_ENDPOINT"] ?? "http://localhost:11434"),
            apiKey: "unused"
        );
    }

    kernelBuilder.Plugins.AddFromType<SensorPlugin>();
    kernelBuilder.Plugins.AddFromType<RAGPlugin>();
    kernelBuilder.Plugins.AddFromType<PredictionPlugin>();
    kernelBuilder.Plugins.AddFromType<ReportPlugin>();

    return kernelBuilder.Build();
});

// Services
builder.Services.AddHttpClient<INotifierService, NotifierService>();
builder.Services.AddSingleton<IAnalysisJobService, AnalysisJobService>();
builder.Services.AddHostedService<AnalysisResultConsumer>();

builder.Services.AddControllers();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

- [ ] **Step 13: Verify build**

```bash
cd FractureGuard.Api
dotnet build
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 14: Commit**

```bash
git add FractureGuard.Api/ FractureGuard.Api.Tests/
git commit -m "feat(api): project setup, models, Cosmos DB and vector search services"
```

---

### Task 7: SensorPlugin + RAGPlugin

**Files:**
- Create: `FractureGuard.Api/Plugins/SensorPlugin.cs`
- Create: `FractureGuard.Api/Plugins/RAGPlugin.cs`
- Test: `FractureGuard.Api.Tests/Plugins/SensorPluginTests.cs`

- [ ] **Step 1: Write failing test for SensorPlugin**

```csharp
// FractureGuard.Api.Tests/Plugins/SensorPluginTests.cs
using FluentAssertions;
using FractureGuard.Api.Plugins;

namespace FractureGuard.Api.Tests.Plugins;

public class SensorPluginTests
{
    [Fact]
    public async Task GetCurrentReadings_ReturnsSnapshot()
    {
        var mockHandler = new MockHttpMessageHandler(
            System.Net.HttpStatusCode.OK,
            """{"pressure_psi":847,"pressure_trend_pct":12.3,"flow_rate_bpm":12.4,
                 "flow_rate_variance":0.8,"vibration_g":2.3,"temperature_c":42}"""
        );
        var plugin = new SensorPlugin(new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:3001")
        });

        var result = await plugin.GetCurrentReadingsAsync();

        result.Should().NotBeNull();
        result!.PressurePsi.Should().Be(847);
        result.VibrationG.Should().Be(2.3);
    }
}

public class MockHttpMessageHandler(System.Net.HttpStatusCode status, string body)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });
}
```

- [ ] **Step 2: Run — expect FAIL**

```bash
cd FractureGuard.Api.Tests && dotnet test --filter "SensorPluginTests"
```

Expected: Compile error — `SensorPlugin` not found.

- [ ] **Step 3: Create `Plugins/SensorPlugin.cs`**

```csharp
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Plugins;

public class SensorPlugin(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);

    [KernelFunction, Description("Fetches the latest live sensor readings from the fracking site")]
    public async Task<SensorSnapshot?> GetCurrentReadingsAsync()
    {
        var response = await httpClient.GetAsync("/api/sensors/latest");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SensorSnapshot>(_json);
    }
}
```

- [ ] **Step 4: Create `Plugins/RAGPlugin.cs`**

```csharp
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
```

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test --filter "SensorPluginTests"
```

Expected: 1 test PASS.

- [ ] **Step 6: Commit**

```bash
git add FractureGuard.Api/Plugins/SensorPlugin.cs \
        FractureGuard.Api/Plugins/RAGPlugin.cs \
        FractureGuard.Api.Tests/Plugins/SensorPluginTests.cs
git commit -m "feat(api): SensorPlugin and RAGPlugin with tests"
```

---

### Task 8: PredictionPlugin + AnalysisJobService

**Files:**
- Create: `FractureGuard.Api/Services/IAnalysisJobService.cs`
- Create: `FractureGuard.Api/Services/AnalysisJobService.cs`
- Create: `FractureGuard.Api/Plugins/PredictionPlugin.cs`
- Test: `FractureGuard.Api.Tests/Plugins/PredictionPluginTests.cs`

- [ ] **Step 1: Create `Services/IAnalysisJobService.cs`**

```csharp
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Services;

public interface IAnalysisJobService
{
    Task PublishAsync(AnalysisRequest request);
}
```

- [ ] **Step 2: Create `Services/AnalysisJobService.cs`**

```csharp
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Services;

public class AnalysisJobService : IAnalysisJobService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private const string QueueName = "analysis-requests";

    public AnalysisJobService(IConfiguration config)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RABBITMQ_HOST"] ?? "localhost",
            UserName = config["RABBITMQ_USER"] ?? "guest",
            Password = config["RABBITMQ_PASS"] ?? "guest",
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
    }

    public Task PublishAsync(AnalysisRequest request)
    {
        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var props = _channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.Persistent = true;
        _channel.BasicPublish("", QueueName, props, body);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
```

- [ ] **Step 3: Write failing test for PredictionPlugin**

```csharp
// FractureGuard.Api.Tests/Plugins/PredictionPluginTests.cs
using Moq;
using FluentAssertions;
using FractureGuard.Api.Plugins;
using FractureGuard.Api.Services;
using FractureGuard.Api.Models;
using System.Security.Claims;

namespace FractureGuard.Api.Tests.Plugins;

public class PredictionPluginTests
{
    private static SensorSnapshot TestSnapshot() => new(
        PressurePsi: 847, PressureTrendPct: 12.3,
        FlowRateBpm: 12.4, FlowRateVariance: 0.8,
        VibrationG: 2.3, TemperatureC: 42
    );

    [Fact]
    public async Task RequestPrediction_WithEngineerRole_PublishesJob()
    {
        var mockJobService = new Mock<IAnalysisJobService>();
        var plugin = new PredictionPlugin(mockJobService.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("roles", "SiteEngineer") }));

        var result = await plugin.RequestPredictionAsync("session-1", TestSnapshot(), principal);

        mockJobService.Verify(s => s.PublishAsync(
            It.Is<AnalysisRequest>(r => r.SessionId == "session-1")), Times.Once);
        result.Should().Contain("simulation");
    }

    [Fact]
    public async Task RequestPrediction_WithoutEngineerRole_ThrowsUnauthorized()
    {
        var mockJobService = new Mock<IAnalysisJobService>();
        var plugin = new PredictionPlugin(mockJobService.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("roles", "SiteOperator") }));

        var act = async () => await plugin.RequestPredictionAsync("session-1", TestSnapshot(), principal);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        mockJobService.Verify(s => s.PublishAsync(It.IsAny<AnalysisRequest>()), Times.Never);
    }
}
```

- [ ] **Step 4: Run — expect FAIL**

```bash
dotnet test --filter "PredictionPluginTests"
```

Expected: Compile error — `PredictionPlugin` not found.

- [ ] **Step 5: Create `Plugins/PredictionPlugin.cs`**

```csharp
using System.ComponentModel;
using System.Security.Claims;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Models;
using FractureGuard.Api.Services;

namespace FractureGuard.Api.Plugins;

public class PredictionPlugin(IAnalysisJobService jobService)
{
    [KernelFunction, Description("Submits a screen-out risk simulation. Requires SiteEngineer role.")]
    public async Task<string> RequestPredictionAsync(
        [Description("The current chat session ID")] string sessionId,
        [Description("Current sensor snapshot")] SensorSnapshot snapshot,
        ClaimsPrincipal caller)
    {
        var roles = caller.FindAll("roles").Select(c => c.Value);
        if (!roles.Contains("SiteEngineer"))
            throw new UnauthorizedAccessException("ML simulations require the SiteEngineer role.");

        await jobService.PublishAsync(new AnalysisRequest(sessionId, snapshot));
        return "Screen-out simulation submitted. I'll push the results to your dashboard when the analysis completes.";
    }
}
```

- [ ] **Step 6: Run tests — expect PASS**

```bash
dotnet test --filter "PredictionPluginTests"
```

Expected: Both tests PASS.

- [ ] **Step 7: Commit**

```bash
git add FractureGuard.Api/Services/IAnalysisJobService.cs \
        FractureGuard.Api/Services/AnalysisJobService.cs \
        FractureGuard.Api/Plugins/PredictionPlugin.cs \
        FractureGuard.Api.Tests/Plugins/PredictionPluginTests.cs
git commit -m "feat(api): PredictionPlugin with role guard and RabbitMQ publisher"
```

---

### Task 9: ReportPlugin + NotifierService + AnalysisResultConsumer

**Files:**
- Create: `FractureGuard.Api/Plugins/ReportPlugin.cs`
- Create: `FractureGuard.Api/Services/INotifierService.cs`
- Create: `FractureGuard.Api/Services/NotifierService.cs`
- Create: `FractureGuard.Api/Services/AnalysisResultConsumer.cs`
- Test: `FractureGuard.Api.Tests/Plugins/ReportPluginTests.cs`

- [ ] **Step 1: Write failing test for ReportPlugin**

```csharp
// FractureGuard.Api.Tests/Plugins/ReportPluginTests.cs
using FluentAssertions;
using Moq;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Plugins;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Tests.Plugins;

public class ReportPluginTests
{
    [Fact]
    public void BuildReportPrompt_IncludesRiskAndFactors()
    {
        var mockKernel = new Mock<Kernel>();
        var plugin = new ReportPlugin(mockKernel.Object);
        var result = new AnalysisResult(
            SessionId: "test",
            RiskPct: 85.0,
            ContributingFactors: ["pressure_trend", "vibration_amplitude"],
            Confidence: 0.91
        );

        var prompt = plugin.BuildReportPrompt(result, "Pressure exceeds threshold per Protocol 4.2");

        prompt.Should().Contain("85");
        prompt.Should().Contain("pressure_trend");
        prompt.Should().Contain("Protocol 4.2");
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

```bash
dotnet test --filter "ReportPluginTests"
```

Expected: Compile error — `ReportPlugin` not found.

- [ ] **Step 3: Create `Plugins/ReportPlugin.cs`**

```csharp
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Plugins;

public class ReportPlugin(Kernel kernel)
{
    public string BuildReportPrompt(AnalysisResult result, string safetyContext) =>
        $"""
        You are a fracking site safety analyst. Generate a concise technical report.

        ML PREDICTION:
        - Screen-out risk: {result.RiskPct}% (confidence {result.Confidence:P0})
        - Primary drivers: {string.Join(", ", result.ContributingFactors)}

        RELEVANT SAFETY PROTOCOLS:
        {safetyContext}

        Write 3-5 sentences: state the risk level, explain the primary drivers in plain English,
        cite the relevant protocol, and give one concrete recommended action.
        """;

    [KernelFunction, Description("Generates a plain-English technical report from ML prediction output")]
    public async Task<string> GenerateReportAsync(AnalysisResult result, string safetyContext = "")
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(BuildReportPrompt(result, safetyContext));

        var response = await chat.GetChatMessageContentAsync(history);
        return response.Content ?? $"Screen-out risk: {result.RiskPct}% — analysis complete.";
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test --filter "ReportPluginTests"
```

Expected: 1 test PASS.

- [ ] **Step 5: Create `Services/INotifierService.cs`**

```csharp
namespace FractureGuard.Api.Services;

public interface INotifierService
{
    Task SendReportAsync(string sessionId, string reportContent);
}
```

- [ ] **Step 6: Create `Services/NotifierService.cs`**

```csharp
namespace FractureGuard.Api.Services;

public class NotifierService(HttpClient httpClient, IConfiguration config) : INotifierService
{
    public async Task SendReportAsync(string sessionId, string reportContent)
    {
        var payload = new { session_id = sessionId, report = new { content = reportContent } };
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{config["NOTIFIER_URL"]}/notify")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-webhook-secret",
            config["NOTIFIER_WEBHOOK_SECRET"] ?? "local-webhook-secret");
        await httpClient.SendAsync(request);
    }
}
```

- [ ] **Step 7: Create `Services/AnalysisResultConsumer.cs`**

```csharp
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using FractureGuard.Api.Models;
using FractureGuard.Api.Plugins;

namespace FractureGuard.Api.Services;

public class AnalysisResultConsumer : BackgroundService
{
    private const string ResultQueue = "analysis-results";
    private readonly IConfiguration _config;
    private readonly IServiceProvider _sp;
    private readonly ILogger<AnalysisResultConsumer> _logger;

    public AnalysisResultConsumer(IConfiguration config, IServiceProvider sp,
        ILogger<AnalysisResultConsumer> logger)
        => (_config, _sp, _logger) = (config, sp, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _config["RABBITMQ_HOST"] ?? "localhost",
                    UserName = _config["RABBITMQ_USER"] ?? "guest",
                    Password = _config["RABBITMQ_PASS"] ?? "guest",
                };
                using var conn = factory.CreateConnection();
                using var ch = conn.CreateModel();
                ch.QueueDeclare(ResultQueue, durable: true, exclusive: false, autoDelete: false);

                var consumer = new EventingBasicConsumer(ch);
                consumer.Received += async (_, ea) =>
                {
                    try
                    {
                        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                        var result = JsonSerializer.Deserialize<AnalysisResult>(body,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

                        using var scope = _sp.CreateScope();
                        var reportPlugin = scope.ServiceProvider.GetRequiredService<ReportPlugin>();
                        var notifier     = scope.ServiceProvider.GetRequiredService<INotifierService>();

                        var report = await reportPlugin.GenerateReportAsync(result);
                        await notifier.SendReportAsync(result.SessionId, report);
                        ch.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process analysis result");
                        ch.BasicNack(ea.DeliveryTag, false, requeue: false);
                    }
                };

                ch.BasicConsume(ResultQueue, autoAck: false, consumer);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ consumer disconnected, retrying in 5s");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
```

- [ ] **Step 8: Commit**

```bash
git add FractureGuard.Api/Plugins/ReportPlugin.cs \
        FractureGuard.Api/Services/INotifierService.cs \
        FractureGuard.Api/Services/NotifierService.cs \
        FractureGuard.Api/Services/AnalysisResultConsumer.cs \
        FractureGuard.Api.Tests/Plugins/ReportPluginTests.cs
git commit -m "feat(api): ReportPlugin, NotifierService, and Service Bus result consumer"
```

---

### Task 10: ChatController with SSE streaming

**Files:**
- Create: `FractureGuard.Api/Controllers/ChatController.cs`

- [ ] **Step 1: Create `Controllers/ChatController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using FractureGuard.Api.Infrastructure;
using FractureGuard.Api.Models;
using FractureGuard.Api.Plugins;
using System.Security.Claims;
using System.Text;

namespace FractureGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController(
    Kernel kernel,
    ICosmosDbService cosmosDb,
    SensorPlugin sensorPlugin,
    RAGPlugin ragPlugin,
    PredictionPlugin predictionPlugin) : ControllerBase
{
    [HttpPost]
    public async Task Post([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userId    = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        var session   = await cosmosDb.GetOrCreateSessionAsync(sessionId, userId);

        await cosmosDb.AppendMessageAsync(sessionId,
            new ChatMessage("user", request.Message, DateTimeOffset.UtcNow));

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var snapshot      = await sensorPlugin.GetCurrentReadingsAsync();
        var safetyContext = await ragPlugin.GetSafetyContextAsync(request.Message);

        bool needsPrediction =
            request.Message.Contains("risk", StringComparison.OrdinalIgnoreCase)
            || request.Message.Contains("screen-out", StringComparison.OrdinalIgnoreCase)
            || request.Message.Contains("probability", StringComparison.OrdinalIgnoreCase);

        var history = new ChatHistory();
        history.AddSystemMessage(
            $"""
            You are FractureGuard AI, a safety analyst for a hydraulic fracturing site.
            Current sensor readings: {System.Text.Json.JsonSerializer.Serialize(snapshot)}
            Relevant safety protocols: {safetyContext}
            Be concise and technical. Always cite sensor values when making risk assessments.
            """
        );

        foreach (var msg in session.Messages)
            if (msg.Role == "user") history.AddUserMessage(msg.Content);
            else history.AddAssistantMessage(msg.Content);

        history.AddUserMessage(request.Message);

        if (needsPrediction)
        {
            var ack = await predictionPlugin.RequestPredictionAsync(sessionId, snapshot!, User);
            history.AddAssistantMessage(ack);
            history.AddUserMessage(
                "While the simulation runs, briefly explain what current readings suggest.");
        }

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var sb   = new StringBuilder();

        await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(history, cancellationToken: ct))
        {
            var text = chunk.Content ?? string.Empty;
            sb.Append(text);
            await Response.WriteAsync($"data: {text}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await cosmosDb.AppendMessageAsync(sessionId,
            new ChatMessage("assistant", sb.ToString(), DateTimeOffset.UtcNow));
    }

    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetHistory(string sessionId)
    {
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var session = await cosmosDb.GetOrCreateSessionAsync(sessionId, userId);
        return Ok(session.Messages);
    }
}

public record ChatRequest(string Message, string? SessionId);
```

- [ ] **Step 2: Build**

```bash
cd FractureGuard.Api
dotnet build
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Run all tests**

```bash
cd ../FractureGuard.Api.Tests
dotnet test -v normal
```

Expected: All tests PASS.

- [ ] **Step 4: Commit**

```bash
git add FractureGuard.Api/Controllers/ChatController.cs
git commit -m "feat(api): ChatController with SSE streaming and Semantic Kernel orchestration"
```

---

*Phase 3 complete → Phase 5 (Integration) requires this phase*
