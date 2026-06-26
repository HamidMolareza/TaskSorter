using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Tests.Core;

public sealed class GitHubRequestCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_ReusesCachedValueWithinTtl()
    {
        using var memoryCache = CreateMemoryCache();
        var cache = CreateCache(memoryCache);
        var calls = 0;

        var first = await cache.GetOrCreateAsync(
            CreateRequest(),
            _ => Task.FromResult($"value-{++calls}"),
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            CreateRequest(),
            _ => Task.FromResult($"value-{++calls}"),
            CancellationToken.None);

        Assert.Equal("value-1", first.Value);
        Assert.Equal("value-1", second.Value);
        Assert.True(first.FetchedFromSource);
        Assert.False(second.FetchedFromSource);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenRefreshRequested_BypassesAndUpdatesCache()
    {
        using var memoryCache = CreateMemoryCache();
        var cache = CreateCache(memoryCache);
        var calls = 0;

        await cache.GetOrCreateAsync(
            CreateRequest(),
            _ => Task.FromResult($"value-{++calls}"),
            CancellationToken.None);
        var refreshed = await cache.GetOrCreateAsync(
            CreateRequest(refresh: true),
            _ => Task.FromResult($"value-{++calls}"),
            CancellationToken.None);
        var cached = await cache.GetOrCreateAsync(
            CreateRequest(),
            _ => Task.FromResult($"value-{++calls}"),
            CancellationToken.None);

        Assert.Equal("value-2", refreshed.Value);
        Assert.Equal("value-2", cached.Value);
        Assert.True(refreshed.FetchedFromSource);
        Assert.False(cached.FetchedFromSource);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenFetchFails_DoesNotCacheFailure()
    {
        using var memoryCache = CreateMemoryCache();
        var cache = CreateCache(memoryCache);
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync<string>(
            CreateRequest(),
            _ =>
            {
                calls++;
                throw new InvalidOperationException("synthetic failure");
            },
            CancellationToken.None));

        var result = await cache.GetOrCreateAsync(
            CreateRequest(),
            _ => Task.FromResult($"value-{++calls}"),
            CancellationToken.None);

        Assert.Equal("value-2", result.Value);
        Assert.True(result.FetchedFromSource);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenConcurrentMisses_CollapsesToOneFetch()
    {
        using var memoryCache = CreateMemoryCache();
        var cache = CreateCache(memoryCache);
        var calls = 0;
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var requests = Enumerable.Range(0, 5)
            .Select(_ => cache.GetOrCreateAsync(
                CreateRequest(),
                _ =>
                {
                    Interlocked.Increment(ref calls);
                    return source.Task;
                },
                CancellationToken.None))
            .ToArray();

        for (var attempt = 0; attempt < 50 && Volatile.Read(ref calls) == 0; attempt++)
            await Task.Delay(10);

        source.SetResult("value");

        var results = await Task.WhenAll(requests);

        Assert.Equal(1, calls);
        Assert.All(results, result => Assert.Equal("value", result.Value));
        Assert.Equal(1, results.Count(result => result.FetchedFromSource));
        Assert.Equal(4, results.Count(result => !result.FetchedFromSource));
    }

    private static GitHubRequestCache CreateCache(IMemoryCache memoryCache) =>
        new(memoryCache, NullLogger<GitHubRequestCache>.Instance);

    private static MemoryCache CreateMemoryCache() =>
        new(new MemoryCacheOptions { SizeLimit = 1000 });

    private static GitHubCacheRequest CreateRequest(bool refresh = false) =>
        new(
            "github:test:key",
            "repository-issues",
            "owner/repo",
            Enabled: true,
            Refresh: refresh,
            Duration: TimeSpan.FromMinutes(5));
}
