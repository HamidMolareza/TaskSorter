using System.Text.Json;
using TaskSorter.Core.Models;

namespace TaskSorter.Core.Configuration;

public sealed record TaskPriorityFactors
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RepositoryTierPriorityFactors RepositoryTiers { get; init; } = new();
    public StatusPriorityFactors Status { get; init; } = new();
    public SizePriorityFactors Size { get; init; } = new();
    public int AssignmentBonus { get; init; } = 20;
    public int LockPenalty { get; init; } = -100;

    public static TaskPriorityFactors Default { get; } = new();

    public static TaskPriorityFactors FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;

        try
        {
            return JsonSerializer.Deserialize<TaskPriorityFactors>(json, JsonOptions) ?? Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public int GetRepositoryScore(Repository repository) =>
        (repository.Value ?? 0) + RepositoryTiers.GetScore(repository.Tier);

    public int GetStatusScore(string? status) => Status.GetScore(status);

    public int GetSizeScore(string? size) => Size.GetScore(size);
}

public sealed record RepositoryTierPriorityFactors
{
    public int Core { get; init; } = 500;
    public int Active { get; init; } = 300;
    public int Maintenance { get; init; } = 100;
    public int Paused { get; init; } = -200;
    public int Archive { get; init; } = -500;

    public int GetScore(ProjectTier tier) => tier.Name.ToLowerInvariant() switch
    {
        "core" => Core,
        "active" => Active,
        "maintenance" => Maintenance,
        "paused" => Paused,
        "archive" => Archive,
        _ => Active
    };
}

public sealed record StatusPriorityFactors
{
    public int InProgress { get; init; } = 60;
    public int Next { get; init; } = 50;
    public int Waiting { get; init; } = -150;
    public int Blocked { get; init; } = -200;
    public int Default { get; init; } = 0;

    public int GetScore(string? status) => status?.ToLowerInvariant() switch
    {
        "in-progress" => InProgress,
        "next" => Next,
        "waiting" => Waiting,
        "blocked" => Blocked,
        _ => Default
    };
}

public sealed record SizePriorityFactors
{
    public int Small { get; init; } = 30;
    public int Medium { get; init; } = 15;
    public int Large { get; init; } = -10;
    public int Default { get; init; } = 0;

    public int GetScore(string? size) => size?.ToLowerInvariant() switch
    {
        "s" => Small,
        "m" => Medium,
        "l" => Large,
        _ => Default
    };
}
