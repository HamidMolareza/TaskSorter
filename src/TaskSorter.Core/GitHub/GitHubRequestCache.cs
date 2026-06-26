using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace TaskSorter.Core.GitHub;

public sealed class GitHubRequestCache(
    IMemoryCache cache,
    ILogger<GitHubRequestCache> logger) : IGitHubRequestCache
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<GitHubCacheResult<T>> GetOrCreateAsync<T>(
        GitHubCacheRequest request,
        Func<CancellationToken, Task<T>> fetch,
        CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Target);

        if (!request.Enabled || request.Duration <= TimeSpan.Zero)
        {
            logger.LogInformation(
                "GitHubCacheDisabled for {GitHubOperation} {GitHubCacheTarget}.",
                request.Operation,
                request.Target);
            return new GitHubCacheResult<T>(await fetch(cancellationToken), FetchedFromSource: true);
        }

        if (!request.Refresh && cache.TryGetValue<T>(request.Key, out var cachedValue) && cachedValue is not null)
        {
            logger.LogInformation(
                "GitHubCacheHit for {GitHubOperation} {GitHubCacheTarget}.",
                request.Operation,
                request.Target);
            return new GitHubCacheResult<T>(cachedValue, FetchedFromSource: false);
        }

        if (request.Refresh)
        {
            logger.LogInformation(
                "GitHubCacheBypassed for {GitHubOperation} {GitHubCacheTarget} because refresh was requested.",
                request.Operation,
                request.Target);
        }
        else
        {
            logger.LogInformation(
                "GitHubCacheMiss for {GitHubOperation} {GitHubCacheTarget}.",
                request.Operation,
                request.Target);
        }

        var gate = _locks.GetOrAdd(request.Key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            if (!request.Refresh && cache.TryGetValue<T>(request.Key, out cachedValue) && cachedValue is not null)
            {
                logger.LogInformation(
                    "GitHubCacheHitAfterWait for {GitHubOperation} {GitHubCacheTarget}.",
                    request.Operation,
                    request.Target);
                return new GitHubCacheResult<T>(cachedValue, FetchedFromSource: false);
            }

            var freshValue = await fetch(cancellationToken);
            cache.Set(
                request.Key,
                freshValue,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = request.Duration,
                    Size = 1
                });

            logger.LogInformation(
                "GitHubCacheStored for {GitHubOperation} {GitHubCacheTarget} for {GitHubCacheDurationSeconds} second(s).",
                request.Operation,
                request.Target,
                request.Duration.TotalSeconds);

            return new GitHubCacheResult<T>(freshValue, FetchedFromSource: true);
        }
        finally
        {
            gate.Release();
        }
    }
}
