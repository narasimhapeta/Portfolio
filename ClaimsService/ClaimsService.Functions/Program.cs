// ClaimsService.Functions/Program.cs
using ClaimsService.Core.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var cosmosConnectionString = context.Configuration["CosmosDbConnection"]!;
        var databaseName = context.Configuration["CosmosDbDatabaseName"] ?? "ClaimsDb";

        services.AddSingleton(_ => new CosmosClient(cosmosConnectionString));
        services.AddSingleton<IClaimRepository>(sp =>
            new ClaimRepository(sp.GetRequiredService<CosmosClient>(), databaseName));
    })
    .Build();

host.Run();
