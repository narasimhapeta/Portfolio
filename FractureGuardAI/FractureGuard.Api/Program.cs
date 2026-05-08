using Microsoft.Identity.Web;
using FractureGuard.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Auth
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// Infrastructure
builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();
builder.Services.AddSingleton<IVectorSearchService, VectorSearchService>();

builder.Services.AddControllers();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
