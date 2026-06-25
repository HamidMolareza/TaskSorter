using TaskSorter.Models;

namespace TaskSorter.Tasks;

public class TaskData {
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
    public string ProjectTier { get; set; } = TaskSorter.Models.ProjectTier.Active.Name;
    public string Status => GetLabelValue("status/") ?? "unplanned";
    public string Size => GetLabelValue("size/") ?? "unsized";
    public bool HasPriorityLabel => Labels.Any(label => label.Name.StartsWith("priority/"));
    public bool IsBlocked => HasLabel("status/blocked") || HasLabel("status/waiting");

    public int CalculateValue(List<Repository> repoPriorities, List<Label> labelsPriorities) {
        var repositoryPriority = repoPriorities
            .Find(repoPriority => repoPriority == this.Repository);
        var repoValue = repositoryPriority?.PriorityScore ?? 0;

        var scoredLabels = this.Labels
            .Where(taskLabel => labelsPriorities.Any(lp => lp == taskLabel))
            .ToList();
        var unscoredLabels = this.Labels
            .Where(taskLabel => labelsPriorities.All(lp => lp != taskLabel))
            .ToList();

        var labelValue = this.Labels.Sum(taskLabel =>
            labelsPriorities.Find(lp => lp == taskLabel)?
                .Value ?? 0
        );

        var statusScore = Status switch {
            "in-progress" => 60,
            "next" => 50,
            "waiting" => -150,
            "blocked" => -200,
            _ => 0
        };

        var sizeScore = Size switch {
            "s" => 30,
            "m" => 15,
            "l" => -10,
            _ => 0
        };

        var assignmentScore = this.Assigned ? 20 : 0;
        var lockScore = this.Locked ? -100 : 0;

        RepositoryScore = repoValue;
        ProjectTier = repositoryPriority?.Tier.Name ?? TaskSorter.Models.ProjectTier.Active.Name;
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

    private string? GetLabelValue(string prefix) {
        var label = Labels.FirstOrDefault(label => label.Name.StartsWith(prefix));
        return label?.Name[prefix.Length..];
    }
}
