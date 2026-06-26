using System.Text.Json;
namespace TaskSorter.Core.Configuration;

public sealed record TaskPriorityFactors
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public int GetRepositoryScore(Models.Repository repository) => repository.PriorityScore;
}
