using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;
namespace TaskSorter.Core.GitHub;

public interface IGitHubTaskClient
{
    Task<GitHubTaskFetchResult> GetOpenTasksAsync(
        IReadOnlyList<Repository> repositories,
        string githubToken,
        int delayInMilliseconds,
        TimeSpan requestTimeout,
        bool refreshGitHubCache,
        bool quotaOverride,
        ITaskRunProgressReporter? progressReporter,
        CancellationToken cancellationToken);
}
