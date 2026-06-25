namespace TaskSorter.Models;

public sealed record ProjectTier(string Name, int Score) {
    private static readonly Dictionary<string, ProjectTier> Values = new(StringComparer.OrdinalIgnoreCase) {
        ["core"] = new ProjectTier("core", 500),
        ["active"] = new ProjectTier("active", 300),
        ["maintenance"] = new ProjectTier("maintenance", 100),
        ["paused"] = new ProjectTier("paused", -200),
        ["archive"] = new ProjectTier("archive", -500)
    };

    public static ProjectTier Active => Values["active"];

    public static ProjectTier? Parse(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return Active;

        return Values.GetValueOrDefault(value.Trim());
    }

    public static string ValidNames => string.Join(", ", Values.Keys);
}
