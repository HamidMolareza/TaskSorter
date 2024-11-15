using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using OnRail;
using OnRail.ResultDetails.Errors;
using TaskSorter.Helpers;

namespace TaskSorter;

public class App(ILogger<App> logger, AppSettings settings, IFileSystem fileSystem) {
    public async Task<Result> RunAsync() {
        return EnsureInputsAreValid();
    private Result SetGithubTokenIfMissing() {
        if (!string.IsNullOrWhiteSpace(settings.GithubToken))
            return Result.Ok();

        settings.GithubTokenEnvName ??= AppSettings.DefaultGithubTokenEnvName;

        var githubEnv = Environment.GetEnvironmentVariable(settings.GithubTokenEnvName);
        if (string.IsNullOrWhiteSpace(githubEnv)) {
            return Result.Fail(new ValidationError(
                message:
                $"GitHub token is required. You can set it with '{settings.GithubTokenEnvName}' environment or set in `appsettings.json` or give as CLI argument."));
        }

        logger.LogDebug("The GitHub token set with environment.");
        settings.GithubToken = githubEnv;
        return Result.Ok();
    }

    private Result EnsureInputsAreValid() {
        if (!fileSystem.IsFileExist(settings.RepositoryFile)) {
            return Result.Fail(new ValidationError(message: $"{nameof(settings.RepositoryFile)} is required."));
        }

        if (!fileSystem.IsFileExist(settings.LabelsFile)) {
            return Result.Fail(new ValidationError(message: $"{nameof(settings.LabelsFile)} is required."));
        }

        return Result.Ok();
    }
}