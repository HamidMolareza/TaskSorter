using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record ConfigPreviewResponse(
    IReadOnlyList<RepositoryPreviewResponse> Repositories,
    IReadOnlyList<LabelPreviewResponse> Labels,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ValidationIssue> Errors)
{
    public static ConfigPreviewResponse FromPreview(ConfigPreview preview, TaskPriorityFactors priorityFactors) => new(
        preview.Repositories
            .Select(repository => new RepositoryPreviewResponse(
                repository.Owner,
                repository.Name,
                repository.ToString(),
                repository.Tier.Name,
                priorityFactors.GetRepositoryScore(repository)))
            .ToList(),
        preview.Labels
            .Select(label => new LabelPreviewResponse(label.Name, label.DisplayName, label.Value ?? 0))
            .ToList(),
        preview.Warnings,
        preview.Errors);
}

public sealed record RepositoryPreviewResponse(
    string Owner,
    string Name,
    string FullName,
    string Tier,
    int PriorityScore);

public sealed record LabelPreviewResponse(string Name, string DisplayName, int Value);
