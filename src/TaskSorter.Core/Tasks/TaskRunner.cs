using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TaskSorter.Core.Configuration;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Core.Tasks;

public sealed class TaskRunner(
    ProfileConfigurationParser parser,
    IGitHubTaskClient gitHubTaskClient,
    TaskRanker taskRanker,
    ILogger<TaskRunner> logger)
{
    public async Task<TaskRunResult> RunAsync(
        ProfileConfiguration configuration,
        string githubToken,
        TimeSpan gitHubRequestTimeout,
        bool refreshGitHubCache,
        bool quotaOverride,
        CancellationToken cancellationToken,
        ITaskRunProgressReporter? progressReporter = null)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(githubToken))
            throw new ProfileConfigurationException([
                new ValidationIssue("githubToken", "GitHub token is required before running a profile.")
            ]);

        try
        {
            await ReportAsync(
                progressReporter,
                new TaskRunProgress(
                    "validating",
                    "Validating profile configuration.",
                    CompletedOperations: 0,
                    TotalOperations: 0),
                cancellationToken);

            var parsed = parser.Parse(configuration);
            var totalOperations = parsed.Repositories.Count + 2;
            logger.LogInformation(
                "ProfileRunStarted with {RepositoryCount} repositories, {LabelCount} labels, task limit {TaskLimit}, delay {DelayInMilliseconds} ms, cache refresh {RefreshGitHubCache}, quota override {GitHubQuotaOverride}.",
                parsed.Repositories.Count,
                parsed.Labels.Count,
                parsed.TaskLimit,
                parsed.DelayInMilliseconds,
                refreshGitHubCache,
                quotaOverride);

            await ReportAsync(
                progressReporter,
                new TaskRunProgress(
                    "preparing",
                    $"Prepared {parsed.Repositories.Count} repository target(s), {parsed.Labels.Count} label rule(s), and top {parsed.TaskLimit}.",
                    CompletedOperations: 0,
                    TotalOperations: totalOperations),
                cancellationToken);

            var fetchResult = await gitHubTaskClient.GetOpenTasksAsync(
                parsed.Repositories,
                githubToken,
                parsed.DelayInMilliseconds,
                gitHubRequestTimeout,
                refreshGitHubCache,
                quotaOverride,
                progressReporter,
                cancellationToken);

            var tasks = fetchResult.Tasks;
            await ReportAsync(
                progressReporter,
                new TaskRunProgress(
                    "ranking",
                    $"Ranking {tasks.Count} cached/fetched task(s) with the current profile parameters.",
                    CompletedOperations: totalOperations,
                    TotalOperations: totalOperations,
                    ItemCount: tasks.Count),
                cancellationToken);

            var rankedTasks = taskRanker.Rank(
                tasks,
                parsed.Repositories,
                parsed.Labels,
                parsed.TaskLimit,
                parsed.PriorityFactors);
            var warnings = parsed.Warnings
                .Concat(BuildTaskWarnings(rankedTasks))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            logger.LogInformation(
                "ProfileRunCompleted in {ElapsedMs} ms with {FetchedTaskCount} fetched task(s), {RankedTaskCount} ranked task(s), and {WarningCount} warning(s).",
                stopwatch.ElapsedMilliseconds,
                tasks.Count,
                rankedTasks.Count,
                warnings.Count);

            await ReportAsync(
                progressReporter,
                new TaskRunProgress(
                    "completed",
                    $"Completed with {rankedTasks.Count} ranked task(s).",
                    CompletedOperations: totalOperations,
                    TotalOperations: totalOperations,
                    ItemCount: rankedTasks.Count),
                cancellationToken);

            return new TaskRunResult(rankedTasks, warnings, fetchResult.Cache, fetchResult.Quota);
        }
        catch (ProfileConfigurationException ex)
        {
            logger.LogWarning(
                "ProfileRunValidationFailed in {ElapsedMs} ms with {ValidationErrorCount} validation error(s).",
                stopwatch.ElapsedMilliseconds,
                ex.Errors.Count);
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("ProfileRunCanceled after {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (GitHubRequestTimeoutException ex)
        {
            logger.LogWarning(
                ex,
                "ProfileRunTimedOut after {ElapsedMs} ms because {GitHubOperation} exceeded {GitHubRequestTimeoutSeconds} second(s).",
                stopwatch.ElapsedMilliseconds,
                ex.Operation,
                ex.Timeout.TotalSeconds);
            throw;
        }
        catch (Exception ex) when (ex is GitHubQuotaProtectedException or GitHubQuotaExceededException)
        {
            logger.LogWarning(
                ex,
                "ProfileRunGitHubQuotaStopped after {ElapsedMs} ms.",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProfileRunFailed after {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
            throw;
        }
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

    private static async ValueTask ReportAsync(
        ITaskRunProgressReporter? progressReporter,
        TaskRunProgress progress,
        CancellationToken cancellationToken)
    {
        if (progressReporter is not null)
            await progressReporter.ReportAsync(progress, cancellationToken);
    }
}
