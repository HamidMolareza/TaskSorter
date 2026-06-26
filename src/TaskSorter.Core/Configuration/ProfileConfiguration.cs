namespace TaskSorter.Core.Configuration;

public sealed record ProfileConfiguration(
    string RepositoryLines,
    string LabelLines,
    int TaskLimit,
    int DelayInMilliseconds,
    TaskPriorityFactors? PriorityFactors = null)
{
    public TaskPriorityFactors EffectivePriorityFactors => PriorityFactors ?? TaskPriorityFactors.Default;
}
