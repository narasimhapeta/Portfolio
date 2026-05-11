// ClaimsService.Api/Program.cs
using Azure.Storage.Blobs;
using ClaimsService.Api.Services;
using ClaimsService.Core.Models;
using ClaimsService.Core.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Cosmos DB
var cosmosConnectionString = builder.Configuration["Azure:CosmosDb:ConnectionString"]!;
var databaseName = builder.Configuration["Azure:CosmosDb:DatabaseName"]!;
builder.Services.AddSingleton(_ => new CosmosClient(cosmosConnectionString));
builder.Services.AddSingleton<IClaimRepository>(sp =>
    new ClaimRepository(sp.GetRequiredService<CosmosClient>(), databaseName));
builder.Services.AddSingleton<IAdjusterRepository>(sp =>
    new AdjusterRepository(sp.GetRequiredService<CosmosClient>(), databaseName));

// Blob Storage
builder.Services.AddSingleton(_ =>
    new BlobServiceClient(builder.Configuration["Azure:BlobStorage:ConnectionString"]!));
builder.Services.AddScoped<IBlobUploadService, BlobUploadService>();

// Business logic
builder.Services.AddScoped<IClaimService, ClaimService>();

// JWT Auth
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Claims Service API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your-jwt-token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});

var app = builder.Build();

await SeedAdjustersAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedAdjustersAsync(IServiceProvider services)
{
    var repo = services.GetRequiredService<IAdjusterRepository>();
    var adjusters = new[]
    {
        new Adjuster { Id = "adj-001", Name = "Jane Smith",    Email = "jane.smith@insurer.com",    IsAvailable = true },
        new Adjuster { Id = "adj-002", Name = "John Doe",      Email = "john.doe@insurer.com",      IsAvailable = true },
        new Adjuster { Id = "adj-003", Name = "Alice Johnson", Email = "alice.johnson@insurer.com", IsAvailable = true }
    };
    foreach (var adjuster in adjusters)
        await repo.UpsertAsync(adjuster);
}
