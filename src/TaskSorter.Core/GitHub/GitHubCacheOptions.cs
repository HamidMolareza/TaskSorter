namespace TaskSorter.Core.GitHub;

public sealed record GitHubCacheOptions(
    bool Enabled,
    TimeSpan Duration,
    int MaxEntries);
