using Microsoft.Identity.Web;
using Microsoft.SemanticKernel;
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
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: "llama3",
            endpoint: new Uri(config["OLLAMA_ENDPOINT"] ?? "http://localhost:11434"),
            apiKey: "unused"
        );
    }

    return kernelBuilder.Build();
});

// Plugins (scoped so they can use scoped services)
builder.Services.AddScoped<FractureGuard.Api.Plugins.SensorPlugin>();
builder.Services.AddScoped<FractureGuard.Api.Plugins.RAGPlugin>();
builder.Services.AddScoped<FractureGuard.Api.Plugins.PredictionPlugin>();
builder.Services.AddScoped<FractureGuard.Api.Plugins.ReportPlugin>();

// Services
builder.Services.AddHttpClient<FractureGuard.Api.Services.INotifierService, FractureGuard.Api.Services.NotifierService>();
builder.Services.AddSingleton<FractureGuard.Api.Services.IAnalysisJobService, FractureGuard.Api.Services.AnalysisJobService>();
builder.Services.AddHostedService<FractureGuard.Api.Services.AnalysisResultConsumer>();

builder.Services.AddControllers();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
