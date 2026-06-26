namespace TaskSorter.Backend.Profiles;

public sealed record TaskRunStreamEvent(
    string Type,
    string Phase,
    string Message,
    int CompletedOperations,
    int TotalOperations,
    string? Operation = null,
    string? Target = null,
    string? Source = null,
    int? ItemCount = null,
    bool? RefreshRequested = null,
    string? ProfileName = null,
    TaskRunQuotaResponse? Quota = null,
    TaskRunResponse? Result = null,
    string? Error = null,
    string? CorrelationId = null);
