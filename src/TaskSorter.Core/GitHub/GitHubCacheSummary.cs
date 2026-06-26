namespace TaskSorter.Core.GitHub;

public sealed record GitHubCacheSummary(
    string Status,
    bool Enabled,
    bool RefreshRequested,
    int DurationSeconds,
    int HitCount,
    int GitHubRequestCount,
    int OperationCount,
    IReadOnlyList<GitHubCacheOperation> Operations)
{
    public static GitHubCacheSummary Disabled(bool refreshRequested, int durationSeconds) => new(
        "disabled",
        Enabled: false,
        refreshRequested,
        durationSeconds,
        HitCount: 0,
        GitHubRequestCount: 0,
        OperationCount: 0,
        Operations: []);
}
