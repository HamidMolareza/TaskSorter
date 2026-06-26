using Microsoft.Extensions.Logging;
using Octokit;

namespace TaskSorter.Core.GitHub;

public sealed class GitHubQuotaService(
    IGitHubQuotaStore quotaStore,
    GitHubQuotaOptions options,
    ILogger<GitHubQuotaService> logger)
{
    private const string CoreResource = "core";

    public async Task<GitHubQuotaSummary> GetCachedSummaryAsync(
        string tokenFingerprint,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetFreshSnapshotAsync(tokenFingerprint, cancellationToken);
        return snapshot is null
            ? GitHubQuotaSummary.Unknown(options, estimatedRequiredRequests, actualGitHubRequestCount)
            : ToSummary(snapshot, estimatedRequiredRequests, actualGitHubRequestCount, "snapshot");
    }

    public async Task<GitHubQuotaSummary> EnsureRequestAllowedAsync(
        GitHubClient client,
        string tokenFingerprint,
        string operation,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        bool quotaOverride,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetFreshSnapshotAsync(tokenFingerprint, cancellationToken);
        var summary = snapshot is null
            ? await FetchRateLimitSummaryAsync(
                client,
                tokenFingerprint,
                estimatedRequiredRequests,
                actualGitHubRequestCount,
                requestTimeout,
                cancellationToken)
            : ToSummary(snapshot, estimatedRequiredRequests, actualGitHubRequestCount, "snapshot");

        if (!options.ProtectionEnabled || quotaOverride || summary.Remaining is null)
            return summary;

        if (summary.Remaining.Value > options.ReserveRequests)
            return summary;

        var protectedSummary = summary.WithStatus("protected");
        logger.LogWarning(
            "GitHubQuotaProtectionBlocked for {GitHubOperation} with {GitHubQuotaRemaining} remaining request(s), reserve {GitHubQuotaReserveRequests}, reset at {GitHubQuotaResetAt}.",
            operation,
            protectedSummary.Remaining,
            options.ReserveRequests,
            protectedSummary.ResetAt);

        throw new GitHubQuotaProtectedException(
            operation,
            protectedSummary,
            CalculateRetryAfter(protectedSummary.ResetAt));
    }

    public async Task<GitHubQuotaSummary> CaptureLastResponseAsync(
        GitHubClient client,
        string tokenFingerprint,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        CancellationToken cancellationToken)
    {
        var rateLimit = client.GetLastApiInfo()?.RateLimit;
        if (rateLimit is null)
            return await GetCachedSummaryAsync(
                tokenFingerprint,
                estimatedRequiredRequests,
                actualGitHubRequestCount,
                cancellationToken);

        var snapshot = ToSnapshot(tokenFingerprint, rateLimit, "headers");
        await StoreSnapshotAsync(snapshot, cancellationToken);
        return ToSummary(snapshot, estimatedRequiredRequests, actualGitHubRequestCount);
    }

    public async Task<GitHubQuotaSummary> CapturePrimaryRateLimitExceptionAsync(
        string tokenFingerprint,
        RateLimitExceededException exception,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        CancellationToken cancellationToken)
    {
        var remaining = Math.Max(0, exception.Remaining);
        var snapshot = new GitHubQuotaSnapshot(
            tokenFingerprint,
            CoreResource,
            Math.Max(0, exception.Limit),
            remaining,
            CalculateUsed(exception.Limit, remaining),
            exception.Reset,
            DateTimeOffset.UtcNow,
            "headers");

        await StoreSnapshotAsync(snapshot, cancellationToken);
        return ToSummary(snapshot, estimatedRequiredRequests, actualGitHubRequestCount)
            .WithStatus(remaining == 0 ? "exhausted" : "low");
    }

    public async Task<GitHubQuotaSummary> CaptureSecondaryRateLimitExceptionAsync(
        GitHubClient client,
        string tokenFingerprint,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        CancellationToken cancellationToken)
    {
        var summary = await CaptureLastResponseAsync(
            client,
            tokenFingerprint,
            estimatedRequiredRequests,
            actualGitHubRequestCount,
            cancellationToken);

        return summary.WithStatus("secondary-limited");
    }

    private async Task<GitHubQuotaSummary> FetchRateLimitSummaryAsync(
        GitHubClient client,
        string tokenFingerprint,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var limits = await client.RateLimit
                .GetRateLimits()
                .WaitAsync(requestTimeout, cancellationToken);
            var rateLimit = limits.Resources?.Core ?? limits.Rate;
            if (rateLimit is null)
                return GitHubQuotaSummary.Unknown(
                    options,
                    estimatedRequiredRequests,
                    actualGitHubRequestCount,
                    "rate-limit-endpoint");

            var snapshot = ToSnapshot(tokenFingerprint, rateLimit, "rate-limit-endpoint");
            await StoreSnapshotAsync(snapshot, cancellationToken);
            logger.LogInformation(
                "GitHubQuotaSnapshotFetched with {GitHubQuotaRemaining}/{GitHubQuotaLimit} request(s) remaining and reset at {GitHubQuotaResetAt}.",
                snapshot.Remaining,
                snapshot.Limit,
                snapshot.ResetAt);
            return ToSummary(snapshot, estimatedRequiredRequests, actualGitHubRequestCount);
        }
        catch (SecondaryRateLimitExceededException ex)
        {
            var quota = await CaptureSecondaryRateLimitExceptionAsync(
                client,
                tokenFingerprint,
                estimatedRequiredRequests,
                actualGitHubRequestCount,
                cancellationToken);
            throw new GitHubQuotaExceededException("rate-limit", quota, null, ex);
        }
        catch (RateLimitExceededException ex)
        {
            var quota = await CapturePrimaryRateLimitExceptionAsync(
                tokenFingerprint,
                ex,
                estimatedRequiredRequests,
                actualGitHubRequestCount,
                cancellationToken);
            throw new GitHubQuotaExceededException("rate-limit", quota, ex.GetRetryAfterTimeSpan(), ex);
        }
        catch (Exception ex) when (ex is ApiException or TimeoutException)
        {
            logger.LogWarning(
                ex,
                "GitHubQuotaSnapshotFetchFailed; continuing without a fresh quota preflight snapshot.");
            return GitHubQuotaSummary.Unknown(
                options,
                estimatedRequiredRequests,
                actualGitHubRequestCount,
                "unavailable");
        }
    }

    private async Task<GitHubQuotaSnapshot?> GetFreshSnapshotAsync(
        string tokenFingerprint,
        CancellationToken cancellationToken)
    {
        var snapshot = await quotaStore.GetAsync(tokenFingerprint, CoreResource, cancellationToken);
        if (snapshot is null)
            return null;

        var now = DateTimeOffset.UtcNow;
        if (now - snapshot.CapturedAt <= options.SnapshotTtl && snapshot.ResetAt > now)
            return snapshot;

        return null;
    }

    private async Task StoreSnapshotAsync(
        GitHubQuotaSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            await quotaStore.UpsertAsync(snapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GitHubQuotaSnapshotStoreFailed; continuing without persisted quota state.");
        }
    }

    private GitHubQuotaSummary ToSummary(
        GitHubQuotaSnapshot snapshot,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        string? sourceOverride = null)
    {
        var status = CalculateStatus(snapshot.Remaining);
        var resetInSeconds = Math.Max(
            0,
            (int)Math.Ceiling((snapshot.ResetAt - DateTimeOffset.UtcNow).TotalSeconds));

        return new GitHubQuotaSummary(
            status,
            options.ProtectionEnabled,
            options.ReserveRequests,
            options.WarningRemaining,
            estimatedRequiredRequests,
            actualGitHubRequestCount,
            snapshot.Limit,
            snapshot.Remaining,
            snapshot.Used,
            snapshot.ResetAt,
            resetInSeconds,
            sourceOverride ?? snapshot.Source);
    }

    private string CalculateStatus(int remaining)
    {
        if (remaining <= 0)
            return "exhausted";

        return remaining <= options.WarningRemaining ? "low" : "ok";
    }

    private static GitHubQuotaSnapshot ToSnapshot(
        string tokenFingerprint,
        RateLimit rateLimit,
        string source)
    {
        var remaining = Math.Max(0, rateLimit.Remaining);
        return new GitHubQuotaSnapshot(
            tokenFingerprint,
            CoreResource,
            Math.Max(0, rateLimit.Limit),
            remaining,
            CalculateUsed(rateLimit.Limit, remaining),
            rateLimit.Reset,
            DateTimeOffset.UtcNow,
            source);
    }

    private static int CalculateUsed(int limit, int remaining) =>
        Math.Max(0, limit - remaining);

    private static TimeSpan? CalculateRetryAfter(DateTimeOffset? resetAt)
    {
        if (resetAt is null)
            return null;

        return resetAt.Value <= DateTimeOffset.UtcNow
            ? TimeSpan.Zero
            : resetAt.Value - DateTimeOffset.UtcNow;
    }
}
