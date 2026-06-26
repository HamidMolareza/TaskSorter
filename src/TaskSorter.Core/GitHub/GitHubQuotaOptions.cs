namespace TaskSorter.Core.GitHub;

public sealed record GitHubQuotaOptions(
    bool ProtectionEnabled,
    int ReserveRequests,
    int WarningRemaining,
    TimeSpan SnapshotTtl);
