using System.Net.Http.Json;
using CustomerPortal.Application.Customers;
using StackExchange.Redis;
using Xunit;

namespace CustomerPortal.ApiTests;

[Collection(nameof(CustomerApiCollection))]
public class CustomerCacheTests(CustomerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateCustomerRequest ValidCreateRequest(string suffix) => new()
    {
        FirstName = "Ada",
        LastName = $"Lovelace{suffix}",
        Email = $"ada.cache.{suffix}@example.com",
        Phone = "555-0100"
    };

    private async Task<bool> CacheKeyExistsAsync(Guid customerId)
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        return await redis.GetDatabase().KeyExistsAsync($"customer:{customerId}");
    }

    [Fact]
    public async Task GetById_PopulatesRedisCache()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        await _client.GetAsync($"/api/v1/customers/{created!.Id}");

        Assert.True(await CacheKeyExistsAsync(created.Id));
    }

    [Fact]
    public async Task Update_RemovesEntryFromRedisCache()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();
        await _client.GetAsync($"/api/v1/customers/{created!.Id}");

        await _client.PutAsJsonAsync($"/api/v1/customers/{created.Id}", new UpdateCustomerRequest
        {
            FirstName = "Grace", LastName = "Hopper", Email = created.Email, Phone = "555-0200"
        });

        Assert.False(await CacheKeyExistsAsync(created.Id));
    }

    [Fact]
    public async Task Deactivate_RemovesEntryFromRedisCache()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();
        await _client.GetAsync($"/api/v1/customers/{created!.Id}");

        await _client.DeleteAsync($"/api/v1/customers/{created.Id}");

        Assert.False(await CacheKeyExistsAsync(created.Id));
    }
}
