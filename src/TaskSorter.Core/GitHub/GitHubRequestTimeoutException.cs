namespace TaskSorter.Core.GitHub;

public sealed class GitHubRequestTimeoutException(
    string operation,
    TimeSpan timeout,
    Exception innerException)
    : TimeoutException($"GitHub request '{operation}' exceeded the configured timeout of {timeout.TotalSeconds:0} seconds.", innerException)
{
    public string Operation { get; } = operation;
    public TimeSpan Timeout { get; } = timeout;
}
