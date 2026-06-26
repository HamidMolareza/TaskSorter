using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record RunProfileRequest(
    string RepositoryLines,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds,
    TaskPriorityFactors? PriorityFactors = null)
{
    public ProfileConfiguration ToConfiguration() => new(
        RepositoryLines,
        LabelLines,
        TaskLimit,
        DelayInMilliseconds,
        PriorityFactors);
}
