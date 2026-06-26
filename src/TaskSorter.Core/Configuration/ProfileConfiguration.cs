namespace TaskSorter.Core.Configuration;

using TaskSorter.Core.Models;

public sealed record ProfileConfiguration(
    string RepositoryLines,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds,
    TaskPriorityFactors? PriorityFactors = null,
    IReadOnlyList<Repository>? ConfiguredRepositories = null,
    IReadOnlyList<Label>? ConfiguredLabels = null)
{
    public TaskPriorityFactors EffectivePriorityFactors => PriorityFactors ?? TaskPriorityFactors.Default;
}
