using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record RunProfileRequest(
    string? LabelLines = null,
    int? TaskLimit = null,
    int? DelayInMilliseconds = null,
    TaskPriorityFactors? PriorityFactors = null)
{
    public ProfileConfiguration ApplyTo(TaskProfile profile) => new(
        profile.RepositoryLines,
        LabelLines ?? profile.LabelLines,
        TaskLimit ?? profile.TaskLimit,
        DelayInMilliseconds ?? profile.DelayInMilliseconds,
        PriorityFactors ?? TaskPriorityFactors.FromJson(profile.PriorityFactorsJson),
        profile.ToConfiguration().ConfiguredRepositories,
        profile.ToConfiguration().ConfiguredLabels);
}
