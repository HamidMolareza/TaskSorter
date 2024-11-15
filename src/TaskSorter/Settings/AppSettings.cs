namespace TaskSorter.Settings;

public class AppSettings {
    public const string AppName = "TaskSorter";
    public const string DefaultGithubTokenEnvName = "GITHUB_TOKEN";
    public string RepositoryFile { get; set; } = default!;
    public string LabelsFile { get; set; } = default!;
    public string GithubToken { get; set; } = default!;
    public string? GithubTokenEnvName { get; set; }
}