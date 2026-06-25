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
    public int StatusScore { get; set; }
    public int SizeScore { get; set; }
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
        StatusScore,
        SizeScore,
        AssignmentScore,
        LockScore);

    public int CalculateValue(IReadOnlyList<Repository> repositoryPriorities, IReadOnlyList<Label> labelPriorities)
    {
        var repositoryPriority = repositoryPriorities.FirstOrDefault(repoPriority => repoPriority == Repository);
        var repoValue = repositoryPriority?.PriorityScore ?? 0;

        var scoredLabels = Labels
            .Where(taskLabel => labelPriorities.Any(priorityLabel => priorityLabel == taskLabel))
            .ToList();
        var unscoredLabels = Labels
            .Where(taskLabel => labelPriorities.All(priorityLabel => priorityLabel != taskLabel))
            .ToList();

        var labelValue = Labels.Sum(taskLabel =>
            labelPriorities.FirstOrDefault(priorityLabel => priorityLabel == taskLabel)?.Value ?? 0);

        var statusScore = Status switch
        {
            "in-progress" => 60,
            "next" => 50,
            "waiting" => -150,
            "blocked" => -200,
            _ => 0
        };

        var sizeScore = Size switch
        {
            "s" => 30,
            "m" => 15,
            "l" => -10,
            _ => 0
        };

        var assignmentScore = Assigned ? 20 : 0;
        var lockScore = Locked ? -100 : 0;

        RepositoryScore = repoValue;
        ProjectTier = repositoryPriority?.Tier.Name ?? Models.ProjectTier.Active.Name;
        LabelScore = labelValue;
        StatusScore = statusScore;
        SizeScore = sizeScore;
        AssignmentScore = assignmentScore;
        LockScore = lockScore;
        ScoredLabels = scoredLabels;
        UnscoredLabels = unscoredLabels;

        return repoValue + labelValue + statusScore + sizeScore + assignmentScore + lockScore;
    }

    private bool HasLabel(string name) =>
        Labels.Any(label => label.Name == name);

    private string? GetLabelValue(string prefix)
    {
        var label = Labels.FirstOrDefault(label => label.Name.StartsWith(prefix));
        return label?.Name[prefix.Length..];
    }
}
