namespace TaskSorter.Backend.Profiles;

public sealed class ProfileLabel
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public TaskProfile Profile { get; set; } = null!;
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public int? SortOrder { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset FirstDiscoveredAt { get; set; }
    public DateTimeOffset? LastDiscoveredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsPending => !IsIgnored && SortOrder is null;
}
