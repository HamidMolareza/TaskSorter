using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaskSorter.Backend.Data;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Backend.GitHub;

public sealed class PersistentGitHubRequestCache(
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory,
    GitHubCacheOptions options,
    ILogger<PersistentGitHubRequestCache> logger) : IGitHubRequestCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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

        if (!request.Refresh && TryGetMemoryValue(request, out T? cachedValue) && cachedValue is not null)
            return new GitHubCacheResult<T>(cachedValue, FetchedFromSource: false);

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
            if (!request.Refresh)
            {
                if (TryGetMemoryValue(request, out cachedValue) && cachedValue is not null)
                    return new GitHubCacheResult<T>(cachedValue, FetchedFromSource: false);

                var persistentValue = await TryGetPersistentValueAsync<T>(request, cancellationToken);
                if (persistentValue.Found)
                    return new GitHubCacheResult<T>(persistentValue.Value!, FetchedFromSource: false);
            }

            var freshValue = await fetch(cancellationToken);
            var expiresAt = DateTimeOffset.UtcNow.Add(request.Duration);
            SetMemoryValue(request, freshValue, request.Duration);
            await StorePersistentValueAsync(request, freshValue, expiresAt, cancellationToken);

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

    private bool TryGetMemoryValue<T>(GitHubCacheRequest request, out T? value)
        where T : notnull
    {
        if (cache.TryGetValue(request.Key, out value) && value is not null)
        {
            logger.LogInformation(
                "GitHubCacheHit for {GitHubOperation} {GitHubCacheTarget} from memory.",
                request.Operation,
                request.Target);
            return true;
        }

        return false;
    }

    private async Task<(bool Found, T? Value)> TryGetPersistentValueAsync<T>(
        GitHubCacheRequest request,
        CancellationToken cancellationToken)
        where T : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await dbContext.GitHubCacheEntries
            .FirstOrDefaultAsync(entry => entry.Key == request.Key, cancellationToken);
        if (entry is null)
            return (false, default);

        var now = DateTimeOffset.UtcNow;
        if (entry.ExpiresAt <= now)
        {
            dbContext.GitHubCacheEntries.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "GitHubCacheExpired for {GitHubOperation} {GitHubCacheTarget}; persisted entry was removed.",
                request.Operation,
                request.Target);
            return (false, default);
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(entry.ValueJson, JsonOptions);
            if (value is null)
                return (false, default);

            SetMemoryValue(request, value, entry.ExpiresAt - now);
            logger.LogInformation(
                "GitHubCacheHit for {GitHubOperation} {GitHubCacheTarget} from database.",
                request.Operation,
                request.Target);
            return (true, value);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            dbContext.GitHubCacheEntries.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                ex,
                "GitHubCacheDeserializeFailed for {GitHubOperation} {GitHubCacheTarget}; persisted entry was removed.",
                request.Operation,
                request.Target);
            return (false, default);
        }
    }

    private async Task StorePersistentValueAsync<T>(
        GitHubCacheRequest request,
        T value,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
        where T : notnull
    {
        try
        {
            var valueJson = JsonSerializer.Serialize(value, JsonOptions);

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            var entry = await dbContext.GitHubCacheEntries
                .FirstOrDefaultAsync(entry => entry.Key == request.Key, cancellationToken);

            if (entry is null)
            {
                dbContext.GitHubCacheEntries.Add(new GitHubCacheEntry
                {
                    Key = request.Key,
                    Operation = request.Operation,
                    Target = request.Target,
                    ValueJson = valueJson,
                    ExpiresAt = expiresAt,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                entry.Operation = request.Operation;
                entry.Target = request.Target;
                entry.ValueJson = valueJson;
                entry.ExpiresAt = expiresAt;
                entry.UpdatedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await PrunePersistentCacheAsync(dbContext, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or DbUpdateException)
        {
            logger.LogWarning(
                ex,
                "GitHubCacheStoreFailed for {GitHubOperation} {GitHubCacheTarget}; continuing with memory cache only.",
                request.Operation,
                request.Target);
        }
    }

    private async Task PrunePersistentCacheAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredEntries = await dbContext.GitHubCacheEntries
            .Where(entry => entry.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        if (expiredEntries.Count > 0)
        {
            dbContext.GitHubCacheEntries.RemoveRange(expiredEntries);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (options.MaxEntries > 0)
        {
            var entryCount = await dbContext.GitHubCacheEntries.CountAsync(cancellationToken);
            var removeCount = entryCount - options.MaxEntries;
            if (removeCount > 0)
            {
                var oldestEntries = await dbContext.GitHubCacheEntries
                    .OrderBy(entry => entry.UpdatedAt)
                    .Take(removeCount)
                    .ToListAsync(cancellationToken);
                dbContext.GitHubCacheEntries.RemoveRange(oldestEntries);
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void SetMemoryValue<T>(GitHubCacheRequest request, T value, TimeSpan duration)
        where T : notnull
    {
        if (duration <= TimeSpan.Zero)
            return;

        cache.Set(
            request.Key,
            value,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration,
                Size = 1
            });
    }
}
