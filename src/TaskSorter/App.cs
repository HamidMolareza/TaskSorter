using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using OnRails;
using OnRails.Extensions.OnFail;
using OnRails.Extensions.OnSuccess;
using OnRails.Extensions.Try;
using OnRails.ResultDetails.Errors.BadRequest;
using TaskSorter.Helpers;
using TaskSorter.Models;
using TaskSorter.Outputs;
using TaskSorter.Settings;
using TaskSorter.Tasks;

namespace TaskSorter;

public class App(ILogger<App> logger, AppSettings settings, IFileSystem fileSystem, TasksService tasksService) {
    public Task<Result> RunAsync() =>
        SetGithubTokenIfMissing()
            .OnSuccess(EnsureInputsAreValid)
            .OnSuccessTee(() => logger.LogDebug("Inputs validated and was valid"))
            .OnSuccess(GetRepositoryPrioritiesFromFile)
            .OnSuccessTee(repos => logger.LogInformation("{repoCount} repository is defined in the {file}.",
                repos.Count, settings.RepositoryFile))
            .OnSuccess(GetSortedTasks)
            .OnSuccessTee(tasks => logger.LogInformation("{count} tasks found.", tasks.Count))
            .OnSuccess(ApplyTaskLimit)
            .OnSuccess(SaveDataAsync);

    private Result SetGithubTokenIfMissing() {
        if (!string.IsNullOrWhiteSpace(settings.GithubToken))
            return Result.Ok();

        settings.GithubTokenEnvName ??= AppSettings.DefaultGithubTokenEnvName;

        var githubEnv = Environment.GetEnvironmentVariable(settings.GithubTokenEnvName);
        if (string.IsNullOrWhiteSpace(githubEnv)) {
            return Result.Fail(new ValidationError(nameof(githubEnv),
                $"GitHub token is required. You can set it with '{settings.GithubTokenEnvName}' environment or set in `appsettings.json` or give as CLI argument."));
        }

        logger.LogDebug("The GitHub token set with environment.");
        settings.GithubToken = githubEnv;
        return Result.Ok();
    }

    private Result EnsureInputsAreValid() {
        if (!fileSystem.IsFileExist(settings.RepositoryFile)) {
            return Result.Fail(new ValidationError(nameof(settings.RepositoryFile), $"{nameof(settings.RepositoryFile)} is required."));
        }

        if (!fileSystem.IsFileExist(settings.LabelsFile)) {
            return Result.Fail(new ValidationError(nameof(settings.LabelsFile), $"{nameof(settings.LabelsFile)} is required."));
        }

        if (settings.OutputTypes.Count == 0) {
            return Result.Fail(new ValidationError(
                nameof(OutputTypes), $"At lease on output type is required. Types: {EnumConverter.EnumToString<OutputTypes>()}"));
        }

        if (settings.DelayInMilliSeconds < 0) {
            return Result.Fail(
                new ValidationError(nameof(settings.DelayInMilliSeconds), $"The delay can not less than 0 ({settings.DelayInMilliSeconds})"));
        }

        if (settings.TaskLimit <= 0) {
            return Result.Fail(
                new ValidationError(nameof(settings.TaskLimit), $"The task limit must be greater than 0 ({settings.TaskLimit})"));
        }

        return Result.Ok();
    }

    private Task<Result<List<Repository>>> GetRepositoryPrioritiesFromFile() =>
        Repository.ReadFileAsync(fileSystem, settings.RepositoryFile);

    private Task<Result<List<TaskData>>> GetSortedTasks(List<Repository> repos) =>
        tasksService.GetAllAsync(repos)
            .OnSuccess(tasks =>
                Label.ReadFileAsync(fileSystem, settings.LabelsFile)
                    .OnSuccess(labels => (tasks, labels))
            )
            .OnSuccess(output => {
                TasksService.CalculateValues(output.tasks, repos, output.labels);
                LogTaskWarnings(output.tasks);
                return output.tasks;
            }).OnSuccess(TasksService.Sort);

    private List<TaskData> ApplyTaskLimit(List<TaskData> tasks) {
        var limitedTasks = tasks.Take(settings.TaskLimit).ToList();
        logger.LogInformation("Report limited to top {taskLimit} task(s).", settings.TaskLimit);
        return limitedTasks;
    }

    private void LogTaskWarnings(List<TaskData> tasks) {
        var missingPriorityCount = tasks.Count(task => !task.HasPriorityLabel);
        if (missingPriorityCount > 0) {
            logger.LogWarning("{count} task(s) do not have a priority label.", missingPriorityCount);
        }

        var unscoredLabels = tasks
            .SelectMany(task => task.UnscoredLabels.Select(label => label.DisplayName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToList();
        if (unscoredLabels.Count == 0)
            return;

        var displayedLabels = string.Join(", ", unscoredLabels.Take(20));
        var moreCount = unscoredLabels.Count - 20;
        if (moreCount > 0)
            displayedLabels += $", +{moreCount} more";

        logger.LogWarning("Ignored {count} unscored label(s): {labels}", unscoredLabels.Count, displayedLabels);
    }

    private async Task SaveDataAsync(List<TaskData> tasks) {
        foreach (var typeStr in settings.OutputTypes) {
            await TryExtensions.Try(() => SaveDataAsync(tasks, typeStr))
                .OnFailTee(result => logger.LogError("Save data for {format} failed: {error}", typeStr,
                    result.Detail?.ToStr() ?? "No more Data!"));
        }
    }

    private async Task SaveDataAsync(List<TaskData> tasks, string typeStr) {
        var type = EnumConverter.TryParseEnum<OutputTypes>(typeStr);
        if (type is null) {
            logger.LogError("The output type ({type}) is not valid. Valid types: {ValidTypes}", typeStr,
                EnumConverter.EnumToString<OutputTypes>());
            return;
        }

        var filePath = await OutputHelpers.SaveAsync(tasks, (OutputTypes)type, settings.OutputDir);

        logger.LogInformation("{typeStr} format saved to {path}", typeStr, filePath);
    }
}
