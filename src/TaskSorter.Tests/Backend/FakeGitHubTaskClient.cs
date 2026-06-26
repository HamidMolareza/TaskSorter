using TaskSorter.Core.GitHub;
using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Tests.Backend;

internal sealed class FakeGitHubTaskClient : IGitHubTaskClient
{
    private readonly object _sync = new();
    private readonly List<bool> _refreshRequests = [];
    private readonly List<bool> _quotaOverrideRequests = [];

    public IReadOnlyList<bool> RefreshRequests
    {
        get
        {
            lock (_sync)
            {
                return [.._refreshRequests];
            }
        }
    }

    public IReadOnlyList<bool> QuotaOverrideRequests
    {
        get
        {
            lock (_sync)
            {
                return [.._quotaOverrideRequests];
            }
        }
    }

    public void ClearRequests()
    {
        lock (_sync)
        {
            _refreshRequests.Clear();
            _quotaOverrideRequests.Clear();
        }
    }

    public Task<GitHubTaskFetchResult> GetRepositoryTasksAsync(
        IReadOnlyList<Repository> repositories,
        string githubToken,
        int delayInMilliseconds,
        TimeSpan requestTimeout,
        bool refreshGitHubCache,
        bool quotaOverride,
        CancellationToken cancellationToken) =>
        GetOpenTasksAsync(
            repositories,
            githubToken,
            delayInMilliseconds,
            requestTimeout,
            refreshGitHubCache,
            quotaOverride,
            progressReporter: null,
            cancellationToken);

    public async Task<GitHubTaskFetchResult> GetOpenTasksAsync(
        IReadOnlyList<Repository> repositories,
        string githubToken,
        int delayInMilliseconds,
        TimeSpan requestTimeout,
        bool refreshGitHubCache,
        bool quotaOverride,
        ITaskRunProgressReporter? progressReporter,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _refreshRequests.Add(refreshGitHubCache);
            _quotaOverrideRequests.Add(quotaOverride);
        }

        if (githubToken == "ghp_fail")
            throw new InvalidOperationException("Synthetic GitHub failure for logging tests.");

        if (githubToken == "ghp_github_timeout")
            throw new GitHubRequestTimeoutException("synthetic-github-request", requestTimeout, new TimeoutException());

        if (githubToken == "ghp_quota_protected")
            throw new GitHubQuotaProtectedException(
                "repository:owner/repo",
                CreateQuota("protected", remaining: 50, actualGitHubRequestCount: 0),
                TimeSpan.FromMinutes(12));

        if (githubToken == "ghp_slow")
            return await SlowResponseAsync(cancellationToken);

        var taskCount = githubToken == "ghp_many" ? 20 : 1;
        List<Label> labels = githubToken == "ghp_label_subset"
            ? [new Label("priority/high")]
            : [new Label("priority/high"), new Label("status/next"), new Label("size/s")];
        var tasks = Enumerable.Range(1, taskCount)
            .Select(index => new TaskData
            {
                Id = index,
                Title = index == 1 ? "Fix failing build" : $"Fix queued task {index}",
                Type = TaskTypes.Issue,
                Repository = new Repository("owner", "repo"),
                Labels = labels,
                Url = $"https://github.com/owner/repo/issues/{index}",
                Assigned = true,
                Locked = false,
                CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddMinutes(index),
                UpdatedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z").AddMinutes(index)
            })
            .ToList();

        var cache = new GitHubCacheSummary(
            refreshGitHubCache ? "refreshed" : "github",
            Enabled: true,
            RefreshRequested: refreshGitHubCache,
            DurationSeconds: 300,
            HitCount: 0,
            GitHubRequestCount: 1,
            OperationCount: 1,
            Operations: [new GitHubCacheOperation("repository-issues", "owner/repo", refreshGitHubCache ? "refresh" : "github")]);

        if (progressReporter is not null)
        {
            await progressReporter.ReportAsync(
                new TaskRunProgress(
                    "github",
                    $"Loaded {tasks.Count} task(s) for owner/repo.",
                    CompletedOperations: 1,
                    TotalOperations: 1,
                    Operation: "repository-issues",
                    Target: "owner/repo",
                    Source: refreshGitHubCache ? "refresh" : "github",
                    ItemCount: tasks.Count,
                    Quota: CreateQuota("ok", remaining: 4900, actualGitHubRequestCount: 1)),
                cancellationToken);
        }

        return new GitHubTaskFetchResult(tasks, cache, CreateQuota("ok", remaining: 4900, actualGitHubRequestCount: 1));
    }

    private static async Task<GitHubTaskFetchResult> SlowResponseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        return new GitHubTaskFetchResult(
            [],
            new GitHubCacheSummary(
                "github",
                Enabled: true,
                RefreshRequested: false,
                DurationSeconds: 300,
                HitCount: 0,
                GitHubRequestCount: 0,
                OperationCount: 0,
                Operations: []),
            CreateQuota("unknown", remaining: null, actualGitHubRequestCount: 0));
    }

    private static GitHubQuotaSummary CreateQuota(
        string status,
        int? remaining,
        int actualGitHubRequestCount) => new(
        status,
        ProtectionEnabled: true,
        ReserveRequests: 50,
        WarningRemaining: 250,
        EstimatedRequiredRequests: 3,
        ActualGitHubRequestCount: actualGitHubRequestCount,
        Limit: remaining is null ? null : 5000,
        remaining,
        Used: remaining is null ? null : 5000 - remaining.Value,
        ResetAt: remaining is null ? null : DateTimeOffset.Parse("2026-01-01T01:00:00Z"),
        ResetInSeconds: remaining is null ? null : 3600,
        Source: remaining is null ? "unavailable" : "headers");
}
