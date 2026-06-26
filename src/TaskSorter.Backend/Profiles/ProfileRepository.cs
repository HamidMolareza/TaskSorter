namespace TaskSorter.Backend.Profiles;

public sealed class ProfileRepository
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public TaskProfile Profile { get; set; } = null!;
    public required string Owner { get; set; }
    public required string Name { get; set; }
    public Guid RepositoryTierId { get; set; }
    public RepositoryTier RepositoryTier { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string FullName => $"{Owner}/{Name}";
}
