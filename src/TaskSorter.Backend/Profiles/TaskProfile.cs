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

    public ProfileConfiguration ToConfiguration() => new(
        RepositoryLines,
        LabelLines,
        TaskLimit,
        DelayInMilliseconds,
        TaskPriorityFactors.FromJson(PriorityFactorsJson),
        Repositories
            .OrderBy(repository => repository.SortOrder)
            .Select((repository, index) => new TaskSorter.Core.Models.Repository(
                repository.Owner,
                repository.Name,
                Repositories.Count - index + 1,
                new TaskSorter.Core.Models.ProjectTier(repository.RepositoryTier.Name, repository.RepositoryTier.Score)))
            .ToList(),
        BuildConfiguredLabels());

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
