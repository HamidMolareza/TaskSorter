namespace TaskSorter.Core.GitHub;

public sealed record GitHubCacheResult<T>(T Value, bool FetchedFromSource)
    where T : notnull;
