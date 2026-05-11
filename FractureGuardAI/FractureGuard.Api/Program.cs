using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Logging;
using System.Text;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Infrastructure;
using FractureGuard.Api.Plugins;
using FractureGuard.Api.Services;

IdentityModelEventSource.ShowPII = true;

var builder = WebApplication.CreateBuilder(args);

// Auth: symmetric dev JWT when DEV_JWT_SECRET is set; Azure AD otherwise
var devJwtSecret = builder.Configuration["DEV_JWT_SECRET"];
if (!string.IsNullOrEmpty(devJwtSecret))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(devJwtSecret));
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key
            };
        });
}
else
{
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
}
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
builder.Services.AddHttpClient<FractureGuard.Api.Plugins.SensorPlugin>(c =>
    c.BaseAddress = new Uri(builder.Configuration["NOTIFIER_URL"] ?? "http://localhost:3001"));
builder.Services.AddScoped<FractureGuard.Api.Plugins.RAGPlugin>();
builder.Services.AddScoped<FractureGuard.Api.Plugins.PredictionPlugin>();
builder.Services.AddScoped<FractureGuard.Api.Plugins.ReportPlugin>();

// Services
builder.Services.AddHttpClient<FractureGuard.Api.Services.INotifierService, FractureGuard.Api.Services.NotifierService>();
builder.Services.AddSingleton<FractureGuard.Api.Services.IAnalysisJobService, FractureGuard.Api.Services.AnalysisJobService>();
builder.Services.AddHostedService<FractureGuard.Api.Services.AnalysisResultConsumer>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

builder.Services.AddControllers();

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
