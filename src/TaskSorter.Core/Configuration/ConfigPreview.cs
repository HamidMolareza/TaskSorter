using TaskSorter.Core.Models;

namespace TaskSorter.Core.Configuration;

public sealed record ConfigPreview(
    IReadOnlyList<Repository> Repositories,
    IReadOnlyList<Label> Labels,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ValidationIssue> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
