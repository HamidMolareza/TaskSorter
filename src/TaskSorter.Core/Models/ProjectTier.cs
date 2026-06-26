namespace TaskSorter.Core.Models;

public sealed record ProjectTier(string Name, int Score)
{
    // Legacy/default values are used only to parse existing text configuration during migration.
    private static readonly Dictionary<string, ProjectTier> LegacyValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["core"] = new("core", 500),
        ["active"] = new("active", 300),
        ["maintenance"] = new("maintenance", 100),
        ["paused"] = new("paused", -200),
        ["archive"] = new("archive", -500)
    };

    public static ProjectTier Active => LegacyValues["active"];

    public static string ValidNames => string.Join(", ", LegacyValues.Keys);

    public static ProjectTier? Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Active
            : LegacyValues.GetValueOrDefault(value.Trim());
}
