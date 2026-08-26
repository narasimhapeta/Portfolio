using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Testcontainers.Redis;
using Xunit;

namespace CustomerPortal.IntegrationTests;

public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder().Build();

    public IDistributedCache CreateCache() =>
        new RedisCache(Options.Create(new RedisCacheOptions { Configuration = _container.GetConnectionString() }));

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(RedisCollection))]
public class RedisCollection : ICollectionFixture<RedisFixture>;
