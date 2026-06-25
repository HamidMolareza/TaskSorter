using TaskSorter.Core.Models;

namespace TaskSorter.Core.Configuration;

public sealed record ParsedProfileConfiguration(
    IReadOnlyList<Repository> Repositories,
    IReadOnlyList<Label> Labels,
    int TaskLimit,
    int DelayInMilliseconds,
    IReadOnlyList<string> Warnings);
