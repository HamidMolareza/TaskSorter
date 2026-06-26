namespace TaskSorter.Core.GitHub;

public sealed record GitHubCacheRequest(
    string Key,
    string Operation,
    string Target,
    bool Enabled,
    bool Refresh,
    TimeSpan Duration);
