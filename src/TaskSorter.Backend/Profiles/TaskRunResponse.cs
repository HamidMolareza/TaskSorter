using TaskSorter.Core.GitHub;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Backend.Profiles;

public sealed record TaskRunResponse(
    IReadOnlyList<TaskItemResponse> Items,
    IReadOnlyList<string> Warnings,
    TaskRunCacheResponse Cache,
    TaskRunQuotaResponse Quota)
{
    public static TaskRunResponse FromResult(TaskRunResult result) => new(
        result.Items.Select(TaskItemResponse.FromTask).ToList(),
        result.Warnings,
        TaskRunCacheResponse.FromSummary(result.Cache),
        TaskRunQuotaResponse.FromSummary(result.Quota));
}

public sealed record TaskRunQuotaResponse(
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
    public static TaskRunQuotaResponse FromSummary(GitHubQuotaSummary summary) => new(
        summary.Status,
        summary.ProtectionEnabled,
        summary.ReserveRequests,
        summary.WarningRemaining,
        summary.EstimatedRequiredRequests,
        summary.ActualGitHubRequestCount,
        summary.Limit,
        summary.Remaining,
        summary.Used,
        summary.ResetAt,
        summary.ResetInSeconds,
        summary.Source);
}

public sealed record TaskRunCacheResponse(
    string Status,
    bool Enabled,
    bool RefreshRequested,
    int DurationSeconds,
    int HitCount,
    int GitHubRequestCount,
    int OperationCount,
    IReadOnlyList<TaskRunCacheOperationResponse> Operations)
{
    public static TaskRunCacheResponse FromSummary(GitHubCacheSummary summary) => new(
        summary.Status,
        summary.Enabled,
        summary.RefreshRequested,
        summary.DurationSeconds,
        summary.HitCount,
        summary.GitHubRequestCount,
        summary.OperationCount,
        summary.Operations.Select(TaskRunCacheOperationResponse.FromOperation).ToList());
}

public sealed record TaskRunCacheOperationResponse(
    string Operation,
    string Target,
    string Source)
{
    public static TaskRunCacheOperationResponse FromOperation(GitHubCacheOperation operation) => new(
        operation.Operation,
        operation.Target,
        operation.Source);
}

public sealed record TaskItemResponse(
    int Rank,
    long Id,
    string Title,
    string Type,
    string Repository,
    string ProjectTier,
    IReadOnlyList<string> Labels,
    string Status,
    string Size,
    string Url,
    bool Assigned,
    bool Locked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int? Score,
    ScoreBreakdown ScoreBreakdown,
    IReadOnlyList<string> UnscoredLabels)
{
    public static TaskItemResponse FromTask(TaskData task, int index) => new(
        index + 1,
        task.Id,
        task.Title,
        task.Type,
        task.Repository.ToString(),
        task.ProjectTier,
        task.Labels.Select(label => label.DisplayName).ToList(),
        task.Status,
        task.Size,
        task.Url,
        task.Assigned,
        task.Locked,
        task.CreatedAt,
        task.UpdatedAt,
        task.Value,
        task.ScoreBreakdown,
        task.UnscoredLabels.Select(label => label.DisplayName).ToList());
}
