using System.CommandLine;
using System.IO.Abstractions;
using TaskSorter.Settings;

namespace TaskSorter;

public static class CommandLine {
    public static Task<int> InvokeAsync(string[] args, AppSettings settings, IFileSystem fileSystem) {
        var repoOption = new Option<string>(
            ["-r", "--repo"],
            () => settings.RepositoryFile,
            "Repository file"
        );
        repoOption.AddValidator(result => {
            var value = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(value))
                result.ErrorMessage = "The repository file is required.";
            else if (!fileSystem.File.Exists(value))
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
            else if (!fileSystem.File.Exists(value))
                result.ErrorMessage = "The labels file is not valid.";
        });

        var githubTokenOption = new Option<string>(
            ["-t", "--token"],
            () => settings.GithubToken,
            "GitHub Token"
        );

        var githubTokenEnvNameOption = new Option<string?>(
            ["-te", "--token-env"],
            () => settings.GithubTokenEnvName,
            "GitHub Token Environment Name"
        );

        var rootCommand =
            new RootCommand(
                "Prioritizes GitHub issues and PRs by project and label using a C# CLI for streamlined task management.") {
                repoOption,
                labelsOption,
                githubTokenOption,
                githubTokenEnvNameOption
            };

        rootCommand.SetHandler((repo, label, githubToken, tokenEnv) => {
            settings.RepositoryFile = repo;
            settings.LabelsFile = label;
            settings.GithubToken = githubToken;
            settings.GithubTokenEnvName = tokenEnv;
        }, repoOption, labelsOption, githubTokenOption, githubTokenEnvNameOption);

        // Invoke the command
        return rootCommand.InvokeAsync(args);
    }
}