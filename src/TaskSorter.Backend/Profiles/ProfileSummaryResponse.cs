namespace TaskSorter.Backend.Profiles;

public sealed record ProfileSummaryResponse(
    Guid Id,
    string Name,
    int TaskLimit,
    int DelayInMilliseconds,
    bool HasGitHubToken,
    DateTimeOffset UpdatedAt)
{
    public static ProfileSummaryResponse FromEntity(TaskProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.TaskLimit,
        profile.DelayInMilliseconds,
        profile.HasGitHubToken,
        profile.UpdatedAt);
}
