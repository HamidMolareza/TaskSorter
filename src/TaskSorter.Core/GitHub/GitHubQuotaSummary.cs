namespace TaskSorter.Core.GitHub;

public sealed record GitHubQuotaSummary(
    string Status,
    bool ProtectionEnabled,
    int ReserveRequests,
    int WarningRemaining,
    int EstimatedRequiredRequests,
    int ActualGitHubRequestCount,
    int? Limit,
    int? Remaining,
    int? Used,
    DateTimeOffset? ResetAt,
    int? ResetInSeconds,
    string Source)
{
    public static GitHubQuotaSummary Unknown(
        GitHubQuotaOptions options,
        int estimatedRequiredRequests,
        int actualGitHubRequestCount,
        string source = "unavailable") => new(
        "unknown",
        options.ProtectionEnabled,
        options.ReserveRequests,
        options.WarningRemaining,
        estimatedRequiredRequests,
        actualGitHubRequestCount,
        Limit: null,
        Remaining: null,
        Used: null,
        ResetAt: null,
        ResetInSeconds: null,
        source);

    public GitHubQuotaSummary WithStatus(string status) => this with { Status = status };

    public GitHubQuotaSummary WithActualGitHubRequestCount(int actualGitHubRequestCount) =>
        this with { ActualGitHubRequestCount = actualGitHubRequestCount };
}
