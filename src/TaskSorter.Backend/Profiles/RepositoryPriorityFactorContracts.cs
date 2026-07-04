namespace TaskSorter.Backend.Profiles;

public sealed record RepositoryPriorityFactorResponse(
    Guid Id,
    string Name,
    string Description,
    int Weight,
    int SortOrder,
    long RowVersion,
    DateTimeOffset UpdatedAt)
{
    public static RepositoryPriorityFactorResponse FromEntity(RepositoryPriorityFactor factor) => new(
        factor.Id,
        factor.Name,
        factor.Description,
        factor.Weight,
        factor.SortOrder,
        factor.RowVersion,
        factor.UpdatedAt);
}

public sealed record SaveRepositoryPriorityFactorRequest(
    string Name,
    string Description,
    int Weight,
    long? RowVersion = null);

public sealed record ReorderRepositoryPriorityFactorsRequest(IReadOnlyList<Guid> FactorIds);

public sealed record RepositoryFactorRatingResponse(
    Guid RepositoryPriorityFactorId,
    int Rating,
    long RowVersion);

public sealed record UpdateRepositoryFactorRatingRequest(int Rating, long RowVersion);
