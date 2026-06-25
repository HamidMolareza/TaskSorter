using TaskSorter.Core.Tasks;

namespace TaskSorter.Backend.Profiles;

public sealed record TaskRunResponse(IReadOnlyList<TaskItemResponse> Items, IReadOnlyList<string> Warnings)
{
    public static TaskRunResponse FromResult(TaskRunResult result) => new(
        result.Items.Select(TaskItemResponse.FromTask).ToList(),
        result.Warnings);
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
