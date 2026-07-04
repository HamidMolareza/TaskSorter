namespace TaskSorter.Backend.Profiles;

public sealed class RepositoryPriorityFactor
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public TaskProfile Profile { get; set; } = null!;
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int SortOrder { get; set; }
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<RepositoryFactorRating> Ratings { get; } = [];
}
