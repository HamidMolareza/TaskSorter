using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record ProfileDetailResponse(
    Guid Id,
    string Name,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds,
    TaskPriorityFactors PriorityFactors,
    bool HasGitHubToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ProfileRepositoryResponse> Repositories,
    IReadOnlyList<ProfileLabelResponse> Labels)
{
    public static ProfileDetailResponse FromEntity(TaskProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.LabelLines,
        profile.TaskLimit,
        profile.DelayInMilliseconds,
        TaskPriorityFactors.FromJson(profile.PriorityFactorsJson),
        profile.HasGitHubToken,
        profile.CreatedAt,
        profile.UpdatedAt,
        profile.Repositories
            .OrderBy(repository => repository.SortOrder)
            .Select((repository, index) => ProfileRepositoryResponse.FromEntity(repository, profile.Repositories.Count - index + 1))
            .ToList(),
        profile.Labels
            .OrderBy(label => label.IsIgnored)
            .ThenBy(label => label.SortOrder is null)
            .ThenBy(label => label.SortOrder)
            .Select(ProfileLabelResponse.FromEntity)
            .ToList());
}
