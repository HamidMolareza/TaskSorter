namespace TaskSorter.Core.GitHub;

public interface IGitHubRequestCache
{
    Task<GitHubCacheResult<T>> GetOrCreateAsync<T>(
        GitHubCacheRequest request,
        Func<CancellationToken, Task<T>> fetch,
        CancellationToken cancellationToken)
        where T : notnull;
}
