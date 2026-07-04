using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed class TaskProfile
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string RepositoryLines { get; set; }
    public required string LabelLines { get; set; }
    public int TaskLimit { get; set; }
    public int DelayInMilliseconds { get; set; }
    public string? PriorityFactorsJson { get; set; }
    public string EncryptedGitHubToken { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool HasGitHubToken => !string.IsNullOrWhiteSpace(EncryptedGitHubToken);
    public List<ProfileRepository> Repositories { get; } = [];
    public List<ProfileLabel> Labels { get; } = [];
    public List<RepositoryPriorityFactor> RepositoryPriorityFactors { get; } = [];

    public ProfileConfiguration ToConfiguration() => new(
        RepositoryLines,
        LabelLines,
        TaskLimit,
        DelayInMilliseconds,
        TaskPriorityFactors.FromJson(PriorityFactorsJson),
        Repositories
            .OrderBy(repository => repository.SortOrder)
            .Select(repository => new TaskSorter.Core.Models.Repository(
                repository.Owner,
                repository.Name,
                CalculateFactorScore(repository, RepositoryPriorityFactors),
                new TaskSorter.Core.Models.ProjectTier(repository.RepositoryTier.Name, repository.RepositoryTier.Score)))
            .ToList(),
        BuildConfiguredLabels());

    public static int CalculateFactorScore(
        ProfileRepository repository,
        IReadOnlyCollection<RepositoryPriorityFactor> factors)
    {
        if (factors.Count == 0)
            return 0;

        var ratings = repository.FactorRatings.ToDictionary(
            rating => rating.RepositoryPriorityFactorId,
            rating => rating.Rating);

        return factors.Sum(factor => (ratings.GetValueOrDefault(factor.Id, 1)) * factor.Weight);
    }

    private IReadOnlyList<TaskSorter.Core.Models.Label> BuildConfiguredLabels()
    {
        var labels = Labels
            .Where(label => !label.IsIgnored && label.SortOrder is not null)
            .OrderBy(label => label.SortOrder)
            .ToList();

        return labels
            .Select((label, index) => new TaskSorter.Core.Models.Label(label.Name, labels.Count - index))
            .ToList();
    }
}
