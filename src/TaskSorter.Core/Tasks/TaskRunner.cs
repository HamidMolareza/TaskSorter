using TaskSorter.Core.Configuration;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Core.Tasks;

public sealed class TaskRunner(
    ProfileConfigurationParser parser,
    IGitHubTaskClient gitHubTaskClient,
    TaskRanker taskRanker)
{
    public async Task<TaskRunResult> RunAsync(
        ProfileConfiguration configuration,
        string githubToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(githubToken))
            throw new ProfileConfigurationException([
                new ValidationIssue("githubToken", "GitHub token is required before running a profile.")
            ]);

        var parsed = parser.Parse(configuration);
        var tasks = await gitHubTaskClient.GetOpenTasksAsync(
            parsed.Repositories,
            githubToken,
            parsed.DelayInMilliseconds,
            cancellationToken);

        var rankedTasks = taskRanker.Rank(tasks, parsed.Repositories, parsed.Labels, parsed.TaskLimit);
        var warnings = parsed.Warnings
            .Concat(BuildTaskWarnings(rankedTasks))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TaskRunResult(rankedTasks, warnings);
    }

    private static IEnumerable<string> BuildTaskWarnings(IReadOnlyList<TaskData> tasks)
    {
        var missingPriorityCount = tasks.Count(task => !task.HasPriorityLabel);
        if (missingPriorityCount > 0)
            yield return $"{missingPriorityCount} task(s) do not have a priority label.";

        var unscoredLabels = tasks
            .SelectMany(task => task.UnscoredLabels.Select(label => label.DisplayName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToList();
        if (unscoredLabels.Count > 0)
            yield return $"Ignored {unscoredLabels.Count} unscored label(s): {string.Join(", ", unscoredLabels.Take(20))}.";
    }
}
