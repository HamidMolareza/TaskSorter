using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record SaveProfileRequest(
    string Name,
    string RepositoryLines,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds,
    string? GitHubToken = null,
    TaskPriorityFactors? PriorityFactors = null)
{
    public ProfileConfiguration ToConfiguration() => new(
        RepositoryLines,
        LabelLines,
        TaskLimit,
        DelayInMilliseconds,
        PriorityFactors);
}
