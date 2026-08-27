using Microsoft.Extensions.Caching.Distributed;

namespace CustomerPortal.UnitTests.TestDoubles;

public class ThrowingDistributedCache : IDistributedCache
{
    public byte[]? Get(string key) => throw new InvalidOperationException("Redis unavailable");

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");

    public void Refresh(string key) => throw new InvalidOperationException("Redis unavailable");

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");

    public void Remove(string key) => throw new InvalidOperationException("Redis unavailable");

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        throw new InvalidOperationException("Redis unavailable");

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");
}
