namespace TaskSorter.Core.GitHub;

public sealed class GitHubQuotaProtectedException(
    string operation,
    GitHubQuotaSummary quota,
    TimeSpan? retryAfter)
    : InvalidOperationException("GitHub quota protection blocked the run before making another GitHub request.")
{
    public string Operation { get; } = operation;
    public GitHubQuotaSummary Quota { get; } = quota;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
