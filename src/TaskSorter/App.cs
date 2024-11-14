using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using OnRail;
using OnRail.ResultDetails.Errors;
using TaskSorter.Helpers;

namespace TaskSorter;

public class App(ILogger<App> logger, AppSettings settings, IFileSystem fileSystem) {
    public async Task<Result> RunAsync() {
        return EnsureInputsAreValid();
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