using AutoInsuranceMind.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',')
            ?? new[] { "http://localhost:3000", "http://localhost:3001" };
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Azure infrastructure services (Singletons — one client per app lifetime)
builder.Services.AddSingleton<AzureBlobService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<AzureSearchService>();

// Core services
builder.Services.AddSingleton<AIService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
