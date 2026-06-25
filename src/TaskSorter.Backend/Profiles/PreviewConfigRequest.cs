using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record PreviewConfigRequest(
    string RepositoryLines,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds)
{
    public ProfileConfiguration ToConfiguration() => new(
        RepositoryLines,
        LabelLines,
        TaskLimit,
        DelayInMilliseconds);
}
