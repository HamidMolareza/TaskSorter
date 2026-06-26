namespace TaskSorter.Backend.Profiles;

public sealed record RepositoryTierResponse(
    Guid Id,
    string Name,
    int Score,
    bool IsDefault,
    int AssignedRepositoryCount,
    DateTimeOffset UpdatedAt)
{
    public static RepositoryTierResponse FromEntity(RepositoryTier tier, int assignedRepositoryCount) => new(
        tier.Id, tier.Name, tier.Score, tier.IsDefault, assignedRepositoryCount, tier.UpdatedAt);
}

public sealed record SaveRepositoryTierRequest(string Name, int Score);

public sealed record ProfileRepositoryResponse(
    Guid Id,
    string Owner,
    string Name,
    string FullName,
    Guid RepositoryTierId,
    string RepositoryTierName,
    int RepositoryTierScore,
    int SortOrder,
    int PositionScore,
    int PriorityScore,
    IReadOnlyList<RepositoryValidationIssue> Validation)
{
    public static ProfileRepositoryResponse FromEntity(ProfileRepository repository, int positionScore) => new(
        repository.Id,
        repository.Owner,
        repository.Name,
        repository.FullName,
        repository.RepositoryTierId,
        repository.RepositoryTier.Name,
        repository.RepositoryTier.Score,
        repository.SortOrder,
        positionScore,
        positionScore + repository.RepositoryTier.Score,
        Validate(repository));

    private static IReadOnlyList<RepositoryValidationIssue> Validate(ProfileRepository repository)
    {
        var issues = new List<RepositoryValidationIssue>();
        if (string.IsNullOrWhiteSpace(repository.Owner) || string.IsNullOrWhiteSpace(repository.Name))
            issues.Add(new("error", "Repository must use an owner and name."));
        if (repository.Owner.Contains('/') || repository.Name.Contains('/'))
            issues.Add(new("error", "Repository must use owner/repository format."));
        if (repository.RepositoryTier is null)
            issues.Add(new("error", "Repository tier is missing."));
        return issues;
    }
}

public sealed record RepositoryValidationIssue(string Severity, string Message);
public sealed record SaveProfileRepositoryRequest(string Owner, string Name, Guid? RepositoryTierId = null);
public sealed record UpdateProfileRepositoryRequest(string Owner, string Name, Guid RepositoryTierId);
public sealed record ReorderProfileRepositoriesRequest(IReadOnlyList<Guid> RepositoryIds);
