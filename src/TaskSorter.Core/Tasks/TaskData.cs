using TaskSorter.Core.Configuration;
using TaskSorter.Core.Models;

namespace TaskSorter.Core.Tasks;

public sealed class TaskData
{
    public required long Id { get; init; }
    public required string Title { get; init; }
    public required string Type { get; init; }
    public required Repository Repository { get; init; }
    public List<Label> Labels { get; init; } = [];
    public required string Url { get; init; }
    public bool Assigned { get; init; }
    public bool Locked { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public int? Value { get; set; }
    public int RepositoryScore { get; set; }
    public int LabelScore { get; set; }
    public int AssignmentScore { get; set; }
    public int LockScore { get; set; }
    public List<Label> ScoredLabels { get; set; } = [];
    public List<Label> UnscoredLabels { get; set; } = [];
    public string ProjectTier { get; set; } = Models.ProjectTier.Active.Name;
    public string Status => GetLabelValue("status/") ?? "unplanned";
    public string Size => GetLabelValue("size/") ?? "unsized";
    public bool HasPriorityLabel => Labels.Any(label => label.Name.StartsWith("priority/"));
    public bool IsBlocked => HasLabel("status/blocked") || HasLabel("status/waiting");
    public ScoreBreakdown ScoreBreakdown => new(
        RepositoryScore,
        LabelScore,
        AssignmentScore,
        LockScore);

    public int CalculateValue(
        IReadOnlyList<Repository> repositoryPriorities,
        IReadOnlyList<Label> labelPriorities,
        TaskPriorityFactors? priorityFactors = null)
    {
        priorityFactors ??= TaskPriorityFactors.Default;
        var repositoryPriority = repositoryPriorities.FirstOrDefault(repoPriority => repoPriority == Repository);
        var repoValue = repositoryPriority is null
            ? 0
            : priorityFactors.GetRepositoryScore(repositoryPriority);

        var scoredLabels = Labels
            .Where(taskLabel => labelPriorities.Any(priorityLabel => priorityLabel == taskLabel))
            .ToList();
        var unscoredLabels = Labels
            .Where(taskLabel => labelPriorities.All(priorityLabel => priorityLabel != taskLabel))
            .ToList();

        var labelValue = Labels.Sum(taskLabel =>
            labelPriorities.FirstOrDefault(priorityLabel => priorityLabel == taskLabel)?.Value ?? 0);

        var assignmentScore = Assigned ? priorityFactors.AssignmentBonus : 0;
        var lockScore = Locked ? priorityFactors.LockPenalty : 0;

        RepositoryScore = repoValue;
        ProjectTier = repositoryPriority?.Tier.Name ?? Models.ProjectTier.Active.Name;
        LabelScore = labelValue;
        AssignmentScore = assignmentScore;
        LockScore = lockScore;
        ScoredLabels = scoredLabels;
        UnscoredLabels = unscoredLabels;

        return repoValue + labelValue + assignmentScore + lockScore;
    }

    private bool HasLabel(string name) =>
        Labels.Any(label => label.Name == name);

    private string? GetLabelValue(string prefix)
    {
        var label = Labels.FirstOrDefault(label => label.Name.StartsWith(prefix));
        return label?.Name[prefix.Length..];
    }
}
