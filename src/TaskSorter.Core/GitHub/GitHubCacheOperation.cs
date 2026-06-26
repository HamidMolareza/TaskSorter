namespace TaskSorter.Core.GitHub;

public sealed record GitHubCacheOperation(
    string Operation,
    string Target,
    string Source);
