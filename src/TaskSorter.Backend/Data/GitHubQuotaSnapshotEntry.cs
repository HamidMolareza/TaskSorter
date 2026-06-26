namespace TaskSorter.Backend.Data;

public sealed class GitHubQuotaSnapshotEntry
{
    public required string TokenFingerprint { get; init; }
    public required string Resource { get; init; }
    public required int Limit { get; set; }
    public required int Remaining { get; set; }
    public required int Used { get; set; }
    public required DateTimeOffset ResetAt { get; set; }
    public required DateTimeOffset CapturedAt { get; set; }
    public required string Source { get; set; }
}
