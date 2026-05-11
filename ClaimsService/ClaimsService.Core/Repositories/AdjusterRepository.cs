// ClaimsService.Core/Repositories/AdjusterRepository.cs
using System.Net;
using ClaimsService.Core.Models;
using Microsoft.Azure.Cosmos;

namespace ClaimsService.Core.Repositories;

public class AdjusterRepository : IAdjusterRepository
{
    private readonly Container _container;

    public AdjusterRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetDatabase(databaseName).GetContainer("adjusters");
    }

    public async Task<Adjuster?> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Adjuster>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Adjuster>> GetAllAsync()
    {
        var iterator = _container.GetItemQueryIterator<Adjuster>(
            new QueryDefinition("SELECT * FROM c"));
        var results = new List<Adjuster>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }

    public async Task UpsertAsync(Adjuster adjuster)
    {
        await _container.UpsertItemAsync(adjuster, new PartitionKey(adjuster.Id));
    }
}
