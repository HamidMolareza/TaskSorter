using TaskSorter.Core.GitHub;

namespace TaskSorter.Core.Tasks;

public sealed record TaskRunProgress(
    string Phase,
    string Message,
    int CompletedOperations,
    int TotalOperations,
    string? Operation = null,
    string? Target = null,
    string? Source = null,
    int? ItemCount = null,
    GitHubQuotaSummary? Quota = null);
