namespace TaskSorter.Core.Tasks;

public sealed record ScoreBreakdown(
    int Repository,
    int Labels,
    int Status,
    int Size,
    int Assignment,
    int Lock);
