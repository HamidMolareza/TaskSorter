using TaskSorter.Core.GitHub;
using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Tests.Backend;

internal sealed class FakeGitHubTaskClient : IGitHubTaskClient
{
    public Task<IReadOnlyList<TaskData>> GetOpenTasksAsync(
        IReadOnlyList<Repository> repositories,
        string githubToken,
        int delayInMilliseconds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskData> tasks =
        [
            new TaskData
            {
                Id = 1,
                Title = "Fix failing build",
                Type = TaskTypes.Issue,
                Repository = new Repository("owner", "repo"),
                Labels = [new Label("priority/high"), new Label("status/next"), new Label("size/s")],
                Url = "https://github.com/owner/repo/issues/1",
                Assigned = true,
                Locked = false,
                CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z")
            }
        ];

        return Task.FromResult(tasks);
    }
}
