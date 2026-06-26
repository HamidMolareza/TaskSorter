namespace TaskSorter.Core.GitHub;

public interface IGitHubQuotaStore
{
    Task<GitHubQuotaSnapshot?> GetAsync(
        string tokenFingerprint,
        string resource,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        GitHubQuotaSnapshot snapshot,
        CancellationToken cancellationToken);
}
