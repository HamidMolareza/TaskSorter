namespace TaskSorter.Backend.Profiles;

public sealed class RepositoryFactorRating
{
    public Guid ProfileRepositoryId { get; set; }
    public ProfileRepository ProfileRepository { get; set; } = null!;
    public Guid RepositoryPriorityFactorId { get; set; }
    public RepositoryPriorityFactor RepositoryPriorityFactor { get; set; } = null!;
    public int Rating { get; set; } = 1;
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
