namespace TaskSorter.Core.Tasks;

public sealed record TaskRunResult(IReadOnlyList<TaskData> Items, IReadOnlyList<string> Warnings);
