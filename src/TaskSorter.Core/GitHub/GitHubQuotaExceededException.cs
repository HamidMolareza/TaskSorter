namespace TaskSorter.Core.GitHub;

public sealed class GitHubQuotaExceededException(
    string operation,
    GitHubQuotaSummary quota,
    TimeSpan? retryAfter,
    Exception innerException)
    : InvalidOperationException("GitHub rejected the request because an API rate limit was reached.", innerException)
{
    public string Operation { get; } = operation;
    public GitHubQuotaSummary Quota { get; } = quota;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
