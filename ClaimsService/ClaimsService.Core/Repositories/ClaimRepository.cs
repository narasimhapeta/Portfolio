// ClaimsService.Core/Repositories/ClaimRepository.cs
using System.Net;
using ClaimsService.Core.Models;
using Microsoft.Azure.Cosmos;

namespace ClaimsService.Core.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly Container _container;

    public ClaimRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetDatabase(databaseName).GetContainer("claims");
    }

    public async Task<Claim?> GetByIdAsync(string id, string customerId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Claim>(id, new PartitionKey(customerId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Claim?> GetByIdCrossPartitionAsync(string id)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id);
        var iterator = _container.GetItemQueryIterator<Claim>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            var item = page.FirstOrDefault();
            if (item != null) return item;
        }
        return null;
    }

    public async Task<IEnumerable<Claim>> GetAllAsync(string? status = null)
    {
        var query = status != null
            ? new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", status)
            : new QueryDefinition("SELECT * FROM c");

        var iterator = _container.GetItemQueryIterator<Claim>(query);
        var results = new List<Claim>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }

    public async Task<Claim> CreateAsync(Claim claim)
    {
        var response = await _container.CreateItemAsync(claim, new PartitionKey(claim.CustomerId));
        return response.Resource;
    }

    public async Task<Claim> UpdateAsync(Claim claim)
    {
        claim.UpdatedAt = DateTime.UtcNow;
        var response = await _container.ReplaceItemAsync(claim, claim.Id, new PartitionKey(claim.CustomerId));
        return response.Resource;
    }
}
