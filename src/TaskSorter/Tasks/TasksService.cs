using Microsoft.Extensions.Logging;
using Octokit;
using OnRails;
using OnRails.Extensions.OnFail;
using OnRails.Extensions.Try;
using TaskSorter.Helpers;
using TaskSorter.Settings;
using Label = TaskSorter.Models.Label;
using Repository = TaskSorter.Models.Repository;

namespace TaskSorter.Tasks;

public class TasksService(ILogger<TasksService> logger, AppSettings settings) {
    public Task<Result<List<TaskData>>> GetAllAsync(List<Repository> repositories) =>
        TryExtensions.Try(async () => {
            var client = new GitHubClient(new ProductHeaderValue(AppSettings.AppName)) {
                Credentials = new Credentials(settings.GithubToken)
            };

            await LogInformationUserName(client);

            var allTasks = await GetAllIssueAndPrTasksAsync(client, repositories);

            await LogInfoRateLimit(client);

            return allTasks;
        });

    private async Task LogInformationUserName(IGitHubClient client) {
        var user = await client.User.Current();
        logger.LogInformation("User: {user}", user.Login);
    }

    private async Task LogInfoRateLimit(IGitHubClient client) {
        var rateLimit = await client.RateLimit.GetRateLimits();
        logger.LogInformation("{RateLimit}", rateLimit.Rate.ToStr());
    }

    public static List<TaskData> Sort(IEnumerable<TaskData> tasks) => tasks
        .OrderByDescending(task => task.Value)
        .ThenBy(task => task.IsBlocked)
        .ThenByDescending(task => task.Assigned)
        .ThenByDescending(task => task.UpdatedAt)
        .ThenBy(task => task.CreatedAt)
        .ThenBy(task => task.Locked)
        .ToList();

    public static void CalculateValues(List<TaskData> tasks,
        List<Repository> repoPriority, List<Label> labelsPriority) {
        foreach (var task in tasks) {
            task.Value = task.CalculateValue(repoPriority, labelsPriority);
        }
    }

    private async Task<List<TaskData>> GetAllIssueAndPrTasksAsync(IGitHubClient client,
        List<Repository> repositories) {
        var tasks = new List<TaskData>();

        logger.LogInformation("Get all issues and PRs related to user...");
        var userIssues = await client.Issue.GetAllForCurrent(new RepositoryIssueRequest {
            Filter = IssueFilter.All,
            State = ItemStateFilter.Open
        }) ?? [];

        tasks.AddRange(MapToTaskData(userIssues));

        logger.LogDebug("Delay {delay}...", settings.DelayInMilliSeconds);
        await Task.Delay(settings.DelayInMilliSeconds);

        foreach (var repo in repositories) {
            logger.LogInformation("Get issues and PRs of {repo}", repo.ToString());
            var issues = await client.Issue.GetAllForRepository(repo.Owner, repo.Name, new RepositoryIssueRequest {
                Filter = IssueFilter.All,
                State = ItemStateFilter.Open
            });

            tasks.AddRange(MapToTaskData(issues));

            logger.LogDebug("Delay {delay}...", settings.DelayInMilliSeconds);
            await Task.Delay(settings.DelayInMilliSeconds);
        }

        return tasks.DistinctBy(task => task.Id)
            .DistinctBy(task => task.Url)
            .ToList();
    }

    private IEnumerable<TaskData> MapToTaskData(IEnumerable<Issue> userIssues) {
        var results = userIssues.Select(MapToTaskData).ToList();

        var errors = results.Where(e => e is { Success: false, Detail: not null })
            .Select(e => e.Detail!.ToStr()).ToList();
        if (errors.Count > 0)
            logger.LogError("{error}", string.Join('\n', errors));

        var taskData = results.Where(e => e.Success).Select(e => e.Value!);
        return taskData;
    }

    private static Result<TaskData> MapToTaskData(Issue issue) =>
        TryExtensions.Try(() => new TaskData {
            Id = issue.Id,
            Title = issue.Title,
            Type = issue.PullRequest is null ? TaskTypes.Issue : TaskTypes.PullRequest,
            Repository =
                new Repository(issue.Repository?.Owner?.Login ?? "", issue.Repository?.Name ?? "empty"),
            Labels = issue.Labels?.Select(label => new Label(label.Name)).ToList() ?? [],
            Url = issue.HtmlUrl,
            Assigned = issue.Assignee is not null,
            Locked = issue.Locked,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt,
        }).OnFailAddMoreDetails(new { issue.Id, issue.Title, issue.HtmlUrl });
}
