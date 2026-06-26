namespace TaskSorter.Backend.Profiles;

public sealed class RepositoryTier
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public int Score { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ProfileRepository> ProfileRepositories { get; } = [];
}
