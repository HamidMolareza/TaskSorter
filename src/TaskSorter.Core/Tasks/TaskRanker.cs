using TaskSorter.Core.Configuration;
using TaskSorter.Core.Models;

namespace TaskSorter.Core.Tasks;

public sealed class TaskRanker
{
    public IReadOnlyList<TaskData> Rank(
        IEnumerable<TaskData> tasks,
        IReadOnlyList<Repository> repositories,
        IReadOnlyList<Label> labels,
        int taskLimit,
        TaskPriorityFactors? priorityFactors = null)
    {
        var rankedTasks = tasks.ToList();
        foreach (var task in rankedTasks)
            task.Value = task.CalculateValue(repositories, labels, priorityFactors);

        return rankedTasks
            .OrderByDescending(task => task.Value)
            .ThenBy(task => task.IsBlocked)
            .ThenByDescending(task => task.Assigned)
            .ThenByDescending(task => task.UpdatedAt)
            .ThenBy(task => task.CreatedAt)
            .ThenBy(task => task.Locked)
            .Take(taskLimit)
            .ToList();
    }
}
