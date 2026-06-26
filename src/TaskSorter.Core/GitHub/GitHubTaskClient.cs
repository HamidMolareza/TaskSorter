using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Octokit;
using TaskSorter.Core.Configuration;
using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;
using Label = TaskSorter.Core.Models.Label;
using Repository = TaskSorter.Core.Models.Repository;

namespace TaskSorter.Core.GitHub;

public sealed class GitHubTaskClient(
    IGitHubRequestCache gitHubRequestCache,
    GitHubCacheOptions cacheOptions,
    GitHubQuotaService gitHubQuotaService,
    ILogger<GitHubTaskClient> logger) : IGitHubTaskClient
{
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
        if (requestTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(requestTimeout), "GitHub request timeout must be greater than zero.");

        logger.LogInformation(
            "GitHubTaskFetchStarted with cache enabled {GitHubCacheEnabled}, cache duration {GitHubCacheDurationSeconds} second(s), cache refresh {RefreshGitHubCache}, quota override {GitHubQuotaOverride}.",
            cacheOptions.Enabled,
            cacheOptions.Duration.TotalSeconds,
            refreshGitHubCache,
            quotaOverride);

        var client = new GitHubClient(new ProductHeaderValue(AppDefaults.AppName))
        {
            Credentials = new Credentials(githubToken)
        };
        var tokenFingerprint = CreateTokenFingerprint(githubToken);
        var cacheOperations = new List<GitHubCacheOperation>();
        var totalOperations = repositories.Count + 2;
        var quotaState = new GitHubQuotaRunState(
            tokenFingerprint,
            estimatedRequiredRequests: totalOperations,
            quotaOverride,
            await gitHubQuotaService.GetCachedSummaryAsync(
                tokenFingerprint,
                totalOperations,
                actualGitHubRequestCount: 0,
                cancellationToken));

        await ReportAsync(
            progressReporter,
            new TaskRunProgress(
                "github",
                "Resolving authenticated GitHub user.",
                CompletedOperations: 0,
                TotalOperations: totalOperations,
                Operation: "current-user",
                Target: "authenticated-user"),
            cancellationToken);

        var currentUser = await gitHubRequestCache.GetOrCreateAsync(
            CreateCacheRequest(
                $"github:current-user:{tokenFingerprint}",
                "current-user",
                "authenticated-user",
                refreshGitHubCache),
            token => ExecuteGitHubRequestAsync(
                "current-user",
                requestTimeout,
                token,
                client,
                quotaState,
                async () =>
                {
                    var user = await client.User.Current();
                    return new CachedGitHubUser(user.Login);
                }),
            cancellationToken);
        var currentUserOperation = ToCacheOperation("current-user", "authenticated-user", currentUser, refreshGitHubCache);
        cacheOperations.Add(currentUserOperation);
        await ReportAsync(
            progressReporter,
            new TaskRunProgress(
                "github",
                $"Resolved authenticated GitHub user from {currentUserOperation.Source}.",
                CompletedOperations: 1,
                TotalOperations: totalOperations,
                Operation: currentUserOperation.Operation,
                Target: currentUserOperation.Target,
                Source: currentUserOperation.Source,
                Quota: quotaState.Summary),
            cancellationToken);
        logger.LogInformation(
            "GitHubCurrentUserResolved for {GitHubUser} from {GitHubFetchSource}.",
            currentUser.Value.Login,
            ToFetchSource(currentUser));

        var tasks = new List<TaskData>();
        await ReportAsync(
            progressReporter,
            new TaskRunProgress(
                "github",
                $"Fetching open tasks assigned to or involving {currentUser.Value.Login}.",
                CompletedOperations: 1,
                TotalOperations: totalOperations,
                Operation: "current-user-issues",
                Target: currentUser.Value.Login),
            cancellationToken);
        var userTasks = await FetchCurrentUserTasksAsync(
            client,
            tokenFingerprint,
            currentUser.Value.Login,
            requestTimeout,
            refreshGitHubCache,
            quotaState,
            cancellationToken);
        var userIssuesOperation = ToCacheOperation("current-user-issues", currentUser.Value.Login, userTasks, refreshGitHubCache);
        cacheOperations.Add(userIssuesOperation);
        tasks.AddRange(userTasks.Value);
        await ReportAsync(
            progressReporter,
            new TaskRunProgress(
                "github",
                $"Loaded {userTasks.Value.Count} user task(s) from {userIssuesOperation.Source}.",
                CompletedOperations: 2,
                TotalOperations: totalOperations,
                Operation: userIssuesOperation.Operation,
                Target: userIssuesOperation.Target,
                Source: userIssuesOperation.Source,
                ItemCount: userTasks.Value.Count,
                Quota: quotaState.Summary),
            cancellationToken);

        await DelayAfterSourceFetchAsync(userTasks, delayInMilliseconds, cancellationToken);

        for (var index = 0; index < repositories.Count; index++)
        {
            var repository = repositories[index];
            cancellationToken.ThrowIfCancellationRequested();
            var completedBeforeRepository = index + 2;
            var repositoryTarget = $"{repository.Owner}/{repository.Name}";

            await ReportAsync(
                progressReporter,
                new TaskRunProgress(
                    "github",
                    $"Loading open tasks for {repositoryTarget}.",
                    CompletedOperations: completedBeforeRepository,
                    TotalOperations: totalOperations,
                    Operation: "repository-issues",
                    Target: repositoryTarget),
                cancellationToken);

            var repositoryTasks = await FetchRepositoryTasksAsync(
                client,
                tokenFingerprint,
                repository,
                requestTimeout,
                refreshGitHubCache,
                quotaState,
                cancellationToken);
            var repositoryOperation = ToCacheOperation("repository-issues", repositoryTarget, repositoryTasks, refreshGitHubCache);
            cacheOperations.Add(repositoryOperation);
            tasks.AddRange(repositoryTasks.Value);
            await ReportAsync(
                progressReporter,
                new TaskRunProgress(
                    "github",
                    $"Loaded {repositoryTasks.Value.Count} task(s) for {repositoryTarget} from {repositoryOperation.Source}.",
                    CompletedOperations: completedBeforeRepository + 1,
                    TotalOperations: totalOperations,
                    Operation: repositoryOperation.Operation,
                    Target: repositoryOperation.Target,
                    Source: repositoryOperation.Source,
                    ItemCount: repositoryTasks.Value.Count,
                    Quota: quotaState.Summary),
                cancellationToken);

            await DelayAfterSourceFetchAsync(repositoryTasks, delayInMilliseconds, cancellationToken);
        }

        var deduplicatedTasks = tasks
            .DistinctBy(task => task.Id)
            .DistinctBy(task => task.Url)
            .ToList();
        logger.LogInformation(
            "GitHubTaskFetchCompleted with {FetchedTaskCount} fetched task(s) and {DeduplicatedTaskCount} deduplicated task(s).",
            tasks.Count,
            deduplicatedTasks.Count);

        return new GitHubTaskFetchResult(
            deduplicatedTasks,
            BuildCacheSummary(refreshGitHubCache, cacheOperations),
            quotaState.Summary.WithActualGitHubRequestCount(quotaState.ActualGitHubRequestCount));
    }

    private async Task<GitHubCacheResult<List<TaskData>>> FetchCurrentUserTasksAsync(
        GitHubClient client,
        string tokenFingerprint,
        string user,
        TimeSpan requestTimeout,
        bool refreshGitHubCache,
        GitHubQuotaRunState quotaState,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("GitHubCurrentUserTaskFetchStarted for {GitHubUser}.", user);

        try
        {
            var tasks = await gitHubRequestCache.GetOrCreateAsync(
                CreateCacheRequest(
                    $"github:current-user-issues:{tokenFingerprint}",
                    "current-user-issues",
                    user,
                    refreshGitHubCache),
                token => ExecuteGitHubRequestAsync(
                    "current-user-issues",
                    requestTimeout,
                    token,
                    client,
                    quotaState,
                    async () =>
                    {
                        var issues = await client.Issue.GetAllForCurrent(new RepositoryIssueRequest
                        {
                            Filter = IssueFilter.All,
                            State = ItemStateFilter.Open
                        });
                        return MapToTaskData(issues).ToList();
                    }),
                cancellationToken);

            logger.LogInformation(
                "GitHubCurrentUserTaskFetchCompleted for {GitHubUser} in {ElapsedMs} ms with {FetchedTaskCount} task(s) from {GitHubFetchSource}.",
                user,
                stopwatch.ElapsedMilliseconds,
                tasks.Value.Count,
                ToFetchSource(tasks));
            return tasks;
        }
        catch (ApiException ex)
        {
            logger.LogError(
                ex,
                "GitHubCurrentUserTaskFetchFailed for {GitHubUser} with status {GitHubStatusCode} after {ElapsedMs} ms.",
                user,
                (int)ex.StatusCode,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task<GitHubCacheResult<List<TaskData>>> FetchRepositoryTasksAsync(
        GitHubClient client,
        string tokenFingerprint,
        Repository repository,
        TimeSpan requestTimeout,
        bool refreshGitHubCache,
        GitHubQuotaRunState quotaState,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "GitHubRepositoryTaskFetchStarted for {RepositoryOwner}/{RepositoryName}.",
            repository.Owner,
            repository.Name);

        try
        {
            var tasks = await gitHubRequestCache.GetOrCreateAsync(
                CreateCacheRequest(
                    $"github:repository-issues:{tokenFingerprint}:{Normalize(repository.Owner)}/{Normalize(repository.Name)}",
                    "repository-issues",
                    $"{repository.Owner}/{repository.Name}",
                    refreshGitHubCache),
                token => ExecuteGitHubRequestAsync(
                    $"repository:{repository.Owner}/{repository.Name}",
                    requestTimeout,
                    token,
                    client,
                    quotaState,
                    async () =>
                    {
                        var issues = await client.Issue.GetAllForRepository(repository.Owner, repository.Name, new RepositoryIssueRequest
                        {
                            Filter = IssueFilter.All,
                            State = ItemStateFilter.Open
                        });
                        return MapToTaskData(issues).ToList();
                    }),
                cancellationToken);

            logger.LogInformation(
                "GitHubRepositoryTaskFetchCompleted for {RepositoryOwner}/{RepositoryName} in {ElapsedMs} ms with {FetchedTaskCount} task(s) from {GitHubFetchSource}.",
                repository.Owner,
                repository.Name,
                stopwatch.ElapsedMilliseconds,
                tasks.Value.Count,
                ToFetchSource(tasks));
            return tasks;
        }
        catch (ApiException ex)
        {
            logger.LogError(
                ex,
                "GitHubRepositoryTaskFetchFailed for {RepositoryOwner}/{RepositoryName} with status {GitHubStatusCode} after {ElapsedMs} ms.",
                repository.Owner,
                repository.Name,
                (int)ex.StatusCode,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static Task DelayAfterSourceFetchAsync<T>(
        GitHubCacheResult<T> result,
        int delayInMilliseconds,
        CancellationToken cancellationToken)
        where T : notnull =>
        !result.FetchedFromSource || delayInMilliseconds <= 0
            ? Task.CompletedTask
            : Task.Delay(delayInMilliseconds, cancellationToken);

    private GitHubCacheRequest CreateCacheRequest(
        string key,
        string operation,
        string target,
        bool refreshGitHubCache) =>
        new(
            key,
            operation,
            target,
            cacheOptions.Enabled,
            refreshGitHubCache,
            cacheOptions.Duration);

    private static string CreateTokenFingerprint(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();

    private static string ToFetchSource<T>(GitHubCacheResult<T> result)
        where T : notnull =>
        result.FetchedFromSource ? "github" : "cache";

    private GitHubCacheSummary BuildCacheSummary(
        bool refreshGitHubCache,
        IReadOnlyList<GitHubCacheOperation> operations)
    {
        if (!cacheOptions.Enabled || cacheOptions.Duration <= TimeSpan.Zero)
            return GitHubCacheSummary.Disabled(
                refreshGitHubCache,
                (int)Math.Ceiling(cacheOptions.Duration.TotalSeconds));

        var hitCount = operations.Count(operation => operation.Source == "cache");
        var gitHubRequestCount = operations.Count(operation => operation.Source != "cache");
        var status = refreshGitHubCache
            ? "refreshed"
            : hitCount switch
            {
                0 => "github",
                _ when gitHubRequestCount == 0 => "cache",
                _ => "mixed"
            };

        return new GitHubCacheSummary(
            status,
            Enabled: true,
            RefreshRequested: refreshGitHubCache,
            DurationSeconds: (int)Math.Ceiling(cacheOptions.Duration.TotalSeconds),
            hitCount,
            gitHubRequestCount,
            operations.Count,
            operations);
    }

    private GitHubCacheOperation ToCacheOperation<T>(
        string operation,
        string target,
        GitHubCacheResult<T> result,
        bool refreshGitHubCache)
        where T : notnull =>
        new(
            operation,
            target,
            !cacheOptions.Enabled || cacheOptions.Duration <= TimeSpan.Zero
                ? "disabled"
                : refreshGitHubCache
                    ? "refresh"
                : ToFetchSource(result));

    private async Task<T> ExecuteGitHubRequestAsync<T>(
        string operation,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken,
        GitHubClient client,
        GitHubQuotaRunState quotaState,
        Func<Task<T>> request)
    {
        try
        {
            quotaState.Summary = await gitHubQuotaService.EnsureRequestAllowedAsync(
                client,
                quotaState.TokenFingerprint,
                operation,
                quotaState.EstimatedRequiredRequests,
                quotaState.ActualGitHubRequestCount,
                quotaState.QuotaOverride,
                requestTimeout,
                cancellationToken);

            var result = await request().WaitAsync(requestTimeout, cancellationToken);
            quotaState.ActualGitHubRequestCount++;
            quotaState.Summary = await gitHubQuotaService.CaptureLastResponseAsync(
                client,
                quotaState.TokenFingerprint,
                quotaState.EstimatedRequiredRequests,
                quotaState.ActualGitHubRequestCount,
                cancellationToken);
            return result;
        }
        catch (TimeoutException ex)
        {
            logger.LogError(
                ex,
                "GitHubRequestTimedOut for {GitHubOperation} after {GitHubRequestTimeoutSeconds} second(s).",
                operation,
                requestTimeout.TotalSeconds);
            throw new GitHubRequestTimeoutException(operation, requestTimeout, ex);
        }
        catch (SecondaryRateLimitExceededException ex)
        {
            quotaState.Summary = await gitHubQuotaService.CaptureSecondaryRateLimitExceptionAsync(
                client,
                quotaState.TokenFingerprint,
                quotaState.EstimatedRequiredRequests,
                quotaState.ActualGitHubRequestCount,
                cancellationToken);
            logger.LogWarning(
                ex,
                "GitHubSecondaryRateLimitExceeded for {GitHubOperation}; backing off before more GitHub requests.",
                operation);
            throw new GitHubQuotaExceededException(operation, quotaState.Summary, null, ex);
        }
        catch (RateLimitExceededException ex)
        {
            quotaState.Summary = await gitHubQuotaService.CapturePrimaryRateLimitExceptionAsync(
                quotaState.TokenFingerprint,
                ex,
                quotaState.EstimatedRequiredRequests,
                quotaState.ActualGitHubRequestCount,
                cancellationToken);
            logger.LogWarning(
                ex,
                "GitHubPrimaryRateLimitExceeded for {GitHubOperation} with {GitHubQuotaRemaining}/{GitHubQuotaLimit} request(s) remaining and reset at {GitHubQuotaResetAt}.",
                operation,
                quotaState.Summary.Remaining,
                quotaState.Summary.Limit,
                quotaState.Summary.ResetAt);
            throw new GitHubQuotaExceededException(
                operation,
                quotaState.Summary,
                ex.GetRetryAfterTimeSpan(),
                ex);
        }
    }

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

    private static async ValueTask ReportAsync(
        ITaskRunProgressReporter? progressReporter,
        TaskRunProgress progress,
        CancellationToken cancellationToken)
    {
        if (progressReporter is not null)
            await progressReporter.ReportAsync(progress, cancellationToken);
    }

    private sealed record CachedGitHubUser(string Login);

    private sealed class GitHubQuotaRunState(
        string tokenFingerprint,
        int estimatedRequiredRequests,
        bool quotaOverride,
        GitHubQuotaSummary summary)
    {
        public string TokenFingerprint { get; } = tokenFingerprint;
        public int EstimatedRequiredRequests { get; } = estimatedRequiredRequests;
        public bool QuotaOverride { get; } = quotaOverride;
        public int ActualGitHubRequestCount { get; set; }
        public GitHubQuotaSummary Summary { get; set; } = summary;
    }
}
