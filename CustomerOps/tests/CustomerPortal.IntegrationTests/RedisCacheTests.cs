using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

namespace CustomerPortal.IntegrationTests;

[Collection(nameof(RedisCollection))]
public class RedisCacheTests(RedisFixture fixture)
{
    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredValue()
    {
        var cache = fixture.CreateCache();
        var key = $"test:{Guid.NewGuid()}";
        var value = Encoding.UTF8.GetBytes("hello-redis");

        await cache.SetAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });
        var fetched = await cache.GetAsync(key);

        Assert.Equal(value, fetched);
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheKey()
    {
        var cache = fixture.CreateCache();
        var key = $"test:{Guid.NewGuid()}";
        await cache.SetAsync(key, Encoding.UTF8.GetBytes("value"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });

        await cache.RemoveAsync(key);
        var fetched = await cache.GetAsync(key);

        Assert.Null(fetched);
    }
}
