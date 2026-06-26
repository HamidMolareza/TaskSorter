using TaskSorter.Core.Tasks;

namespace TaskSorter.Core.GitHub;

public sealed record GitHubTaskFetchResult(
    IReadOnlyList<TaskData> Tasks,
    GitHubCacheSummary Cache,
    GitHubQuotaSummary Quota);
