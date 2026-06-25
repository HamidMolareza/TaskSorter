using Microsoft.Extensions.Logging;
using Octokit;
using TaskSorter.Core.Configuration;
using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;
using Label = TaskSorter.Core.Models.Label;
using Repository = TaskSorter.Core.Models.Repository;

namespace TaskSorter.Core.GitHub;

public sealed class GitHubTaskClient(ILogger<GitHubTaskClient> logger) : IGitHubTaskClient
{
    public async Task<IReadOnlyList<TaskData>> GetOpenTasksAsync(
        IReadOnlyList<Repository> repositories,
        string githubToken,
        int delayInMilliseconds,
        CancellationToken cancellationToken)
    {
        var client = new GitHubClient(new ProductHeaderValue(AppDefaults.AppName))
        {
            Credentials = new Credentials(githubToken)
        };

        var currentUser = await client.User.Current();
        logger.LogInformation("Fetching GitHub tasks for {User}.", currentUser.Login);

        var tasks = new List<TaskData>();
        var userIssues = await client.Issue.GetAllForCurrent(new RepositoryIssueRequest
        {
            Filter = IssueFilter.All,
            State = ItemStateFilter.Open
        });
        tasks.AddRange(MapToTaskData(userIssues));

        await DelayAsync(delayInMilliseconds, cancellationToken);

        foreach (var repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Fetching GitHub tasks for {Repository}.", repository);
            var issues = await client.Issue.GetAllForRepository(repository.Owner, repository.Name, new RepositoryIssueRequest
            {
                Filter = IssueFilter.All,
                State = ItemStateFilter.Open
            });
            tasks.AddRange(MapToTaskData(issues));

            await DelayAsync(delayInMilliseconds, cancellationToken);
        }

        return tasks
            .DistinctBy(task => task.Id)
            .DistinctBy(task => task.Url)
            .ToList();
    }

    private static Task DelayAsync(int delayInMilliseconds, CancellationToken cancellationToken) =>
        delayInMilliseconds <= 0
            ? Task.CompletedTask
            : Task.Delay(delayInMilliseconds, cancellationToken);

    private static IEnumerable<TaskData> MapToTaskData(IEnumerable<Issue> issues) =>
        issues.Select(issue => new TaskData
        {
            Id = issue.Id,
            Title = issue.Title,
            Type = issue.PullRequest is null ? TaskTypes.Issue : TaskTypes.PullRequest,
            Repository = new Repository(
                issue.Repository?.Owner?.Login ?? string.Empty,
                issue.Repository?.Name ?? "unknown"),
            Labels = issue.Labels?.Select(label => new Label(label.Name)).ToList() ?? [],
            Url = issue.HtmlUrl,
            Assigned = issue.Assignee is not null,
            Locked = issue.Locked,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt
        });
}
