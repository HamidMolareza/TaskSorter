namespace TaskSorter.Backend.Data;

public sealed class GitHubCacheEntry
{
    public required string Key { get; init; }
    public required string Operation { get; set; }
    public required string Target { get; set; }
    public required string ValueJson { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
}
