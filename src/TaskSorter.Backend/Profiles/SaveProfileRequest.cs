using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed record SaveProfileRequest(
    string Name,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds,
    string? GitHubToken = null,
    TaskPriorityFactors? PriorityFactors = null);
