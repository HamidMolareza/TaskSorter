namespace TaskSorter.Backend.Profiles;

public sealed record RepositoryTierResponse(
    Guid Id,
    string Name,
    int Score,
    bool IsDefault,
    int AssignedRepositoryCount,
    long RowVersion,
    DateTimeOffset UpdatedAt)
{
    public static RepositoryTierResponse FromEntity(RepositoryTier tier, int assignedRepositoryCount) => new(
        tier.Id, tier.Name, tier.Score, tier.IsDefault, assignedRepositoryCount, tier.RowVersion, tier.UpdatedAt);
}

public sealed record SaveRepositoryTierRequest(string Name, int Score, long? RowVersion = null);

public sealed record ProfileRepositoryResponse(
    Guid Id,
    string Owner,
    string Name,
    string FullName,
    Guid RepositoryTierId,
    string RepositoryTierName,
    int RepositoryTierScore,
    int SortOrder,
    long RowVersion,
    int FactorScore,
    int Score,
    IReadOnlyList<RepositoryFactorRatingResponse> Ratings,
    IReadOnlyList<RepositoryValidationIssue> Validation)
{
    public static ProfileRepositoryResponse FromEntity(
        ProfileRepository repository,
        IReadOnlyCollection<RepositoryPriorityFactor> factors) => new(
        repository.Id,
        repository.Owner,
        repository.Name,
        repository.FullName,
        repository.RepositoryTierId,
        repository.RepositoryTier.Name,
        repository.RepositoryTier.Score,
        repository.SortOrder,
        repository.RowVersion,
        TaskProfile.CalculateFactorScore(repository, factors),
        repository.RepositoryTier.Score + TaskProfile.CalculateFactorScore(repository, factors),
        BuildRatingResponses(repository, factors),
        Validate(repository));

    private static IReadOnlyList<RepositoryFactorRatingResponse> BuildRatingResponses(
        ProfileRepository repository,
        IReadOnlyCollection<RepositoryPriorityFactor> factors)
    {
        var ratings = repository.FactorRatings.ToDictionary(rating => rating.RepositoryPriorityFactorId);
        return factors
            .OrderBy(factor => factor.SortOrder)
            .Select(factor => ratings.TryGetValue(factor.Id, out var rating)
                ? new RepositoryFactorRatingResponse(factor.Id, rating.Rating, rating.RowVersion)
                : new RepositoryFactorRatingResponse(factor.Id, 1, 0))
            .ToList();
    }

    private static IReadOnlyList<RepositoryValidationIssue> Validate(ProfileRepository repository)
    {
        var issues = new List<RepositoryValidationIssue>();
        if (!IsRepositoryCoordinatePart(repository.Owner) || !IsRepositoryCoordinatePart(repository.Name))
            issues.Add(new("error", "Repository owner and name are required and may only contain letters, numbers, dots, dashes, or underscores."));
        if (repository.RepositoryTier is null)
            issues.Add(new("error", "Repository tier is missing."));
        return issues;
    }

    private static bool IsRepositoryCoordinatePart(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 100
        && value.All(character =>
            character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9'
            || character is '.' or '_' or '-');
}

public sealed record RepositoryValidationIssue(string Severity, string Message);
public sealed record SaveProfileRepositoryRequest(string Owner, string Name, Guid? RepositoryTierId = null);
public sealed record UpdateProfileRepositoryRequest(string Owner, string Name, Guid RepositoryTierId, long RowVersion);
