namespace TaskSorter.Core.Tasks;

public sealed record ScoreBreakdown(
    int Repository,
    int Labels,
    int Assignment,
    int Lock);
