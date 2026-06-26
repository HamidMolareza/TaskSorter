namespace TaskSorter.Core.GitHub;

public sealed record GitHubQuotaSnapshot(
    string TokenFingerprint,
    string Resource,
    int Limit,
    int Remaining,
    int Used,
    DateTimeOffset ResetAt,
    DateTimeOffset CapturedAt,
    string Source);
