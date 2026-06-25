using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Core.GitHub;

public interface IGitHubTaskClient
{
    Task<IReadOnlyList<TaskData>> GetOpenTasksAsync(
        IReadOnlyList<Repository> repositories,
        string githubToken,
        int delayInMilliseconds,
        CancellationToken cancellationToken);
}
