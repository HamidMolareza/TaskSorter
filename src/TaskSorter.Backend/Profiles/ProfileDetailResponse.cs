using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record ProfileDetailResponse(
    Guid Id,
    string Name,
    string RepositoryLines,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds,
    TaskPriorityFactors PriorityFactors,
    bool HasGitHubToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProfileDetailResponse FromEntity(TaskProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.RepositoryLines,
        profile.LabelLines,
        profile.TaskLimit,
        profile.DelayInMilliseconds,
        profile.ToConfiguration().EffectivePriorityFactors,
        profile.HasGitHubToken,
        profile.CreatedAt,
        profile.UpdatedAt);
}
