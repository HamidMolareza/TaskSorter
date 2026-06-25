using System.CommandLine;
using System.IO.Abstractions;
using TaskSorter.Helpers;
using TaskSorter.Outputs;
using TaskSorter.Settings;

namespace TaskSorter;

public static class CommandLine {
    private static readonly string[] NonRunningArguments = ["--help", "-h", "-?", "--version"];

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
            ["--token-env"],
            () => settings.GithubTokenEnvName,
            "GitHub Token Environment Name"
        );

        var outputTypesOption = new Option<string>(
            ["-ot", "--output-types"],
            () => string.Empty,
            $"Output Types separated with comma: {EnumConverter.EnumToString<OutputTypes>()}"
        );

        var outputDirOption = new Option<string>(
            ["-o", "--output"],
            () => settings.OutputDir,
            "Output Directory"
        );

        var delayOption = new Option<int>(
            ["-d", "--delay"],
            () => settings.DelayInMilliSeconds,
            "Delay between requests in milliseconds"
        );
        delayOption.AddValidator(result => {
            var value = result.GetValueOrDefault<int>();
            if (value < 0)
                result.ErrorMessage = "The delay value can not less that 0";
        });

        var taskLimitOption = new Option<int>(
            ["--top"],
            () => settings.TaskLimit,
            "Maximum number of ranked tasks to include in the report"
        );
        taskLimitOption.AddValidator(result => {
            var value = result.GetValueOrDefault<int>();
            if (value <= 0)
                result.ErrorMessage = "The top value must be greater than 0.";
        });

        var rootCommand =
            new RootCommand(
                "Prioritizes GitHub issues and PRs by project and label using a C# CLI for streamlined task management.") {
                repoOption,
                labelsOption,
                githubTokenOption,
                githubTokenEnvNameOption,
                outputTypesOption,
                outputDirOption,
                delayOption,
                taskLimitOption
            };

        rootCommand.SetHandler((repo, label, githubToken, tokenEnv, outputTypes, outputDir, delay, taskLimit) => {
                settings.RepositoryFile = repo;
                settings.LabelsFile = label;
                settings.GithubToken = githubToken;
                settings.GithubTokenEnvName = tokenEnv;
                settings.OutputDir = outputDir;
                settings.DelayInMilliSeconds = delay;
                settings.TaskLimit = taskLimit;

                if (!string.IsNullOrWhiteSpace(outputTypes)) {
                    var outputTypesList = ParseOutputTypes(outputTypes);
                    settings.OutputTypes.AddRange(outputTypesList);
                    settings.OutputTypes = settings.OutputTypes.Distinct().ToList();
                }
            }, repoOption, labelsOption, githubTokenOption, githubTokenEnvNameOption, outputTypesOption,
            outputDirOption,
            delayOption,
            taskLimitOption);

        // Invoke the command
        return rootCommand.InvokeAsync(args);
    }

    private static IEnumerable<string> ParseOutputTypes(string outputTypes) {
        return outputTypes.Split(',')
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim());
    }

    public static bool ShouldRunApplication(string[] args) =>
        !args.Any(arg => NonRunningArguments.Contains(arg, StringComparer.OrdinalIgnoreCase));
}
