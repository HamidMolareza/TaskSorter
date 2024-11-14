using System.CommandLine;
using ConsoleSharpTemplate.Helpers.FileService;

namespace ConsoleSharpTemplate;

public static class CommandLine {
    public static Task<int> InvokeAsync(string[] args, AppSettings settings, IFileService fileService) {
        var repoOption = new Option<string>(
            ["-r", "--repo"],
            () => settings.RepositoryFile,
            "Repository file"
        );
        repoOption.AddValidator(result => {
            var value = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(value))
                result.ErrorMessage = "The repository file is required.";
            else if (!fileService.Exists(value))
                result.ErrorMessage = "The repository file is not valid.";
        });

        var labelsOption = new Option<string>(
            ["-l", "--labels"],
            () => settings.LabelsFile,
            "Labels file"
        );
        labelsOption.AddValidator(result => {
            var value = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(value))
                result.ErrorMessage = "The labels file is required.";
            else if (!fileService.Exists(value))
                result.ErrorMessage = "The labels file is not valid.";
        });

        var rootCommand =
            new RootCommand(
                "Prioritizes GitHub issues and PRs by project and label using a C# CLI for streamlined task management.") {
                repoOption,
                labelsOption
            };

        rootCommand.SetHandler((repo, label) => {
            settings.RepositoryFile = repo;
            settings.LabelsFile = label;
        }, repoOption, labelsOption);

        // Invoke the command
        return rootCommand.InvokeAsync(args);
    }
}