using System.IO.Abstractions;
using ConsoleSharpTemplate.Helpers;
using Microsoft.Extensions.Logging;

namespace ConsoleSharpTemplate;

public class App(ILogger<App> logger, AppSettings settings, IFileSystem fileSystem) {
    public async Task Run() {
        EnsureInputsAreValid();
        throw new NotImplementedException();
    }

    private void EnsureInputsAreValid() {
        if (!fileSystem.IsFileExist(settings.RepositoryFile)) {
            logger.LogError("{file} is required.", nameof(settings.RepositoryFile));
        }

        if (!fileSystem.IsFileExist(settings.LabelsFile)) {
            logger.LogError("{file} is required.", nameof(settings.LabelsFile));
        }
    }
}