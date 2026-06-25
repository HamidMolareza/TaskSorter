namespace TaskSorter.Core.Models;

public sealed class Label(string name, int? value = null) : IEquatable<Label>
{
    public string Name { get; init; } = NormalizeName(name);
    public string DisplayName { get; init; } = name;
    public int? Value { get; init; } = value;

    public bool Equals(Label? other) =>
        other is not null && Name == other.Name;

    public override bool Equals(object? obj) =>
        Equals(obj as Label);

    public override int GetHashCode() =>
        HashCode.Combine(Name);

    public static bool operator ==(Label? left, Label? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Label? left, Label? right) =>
        !(left == right);

    private static string NormalizeName(string value)
    {
        var name = value.Trim().ToLowerInvariant();

        return name switch
        {
            "priority-critical" => "priority/critical",
            "priority-high" => "priority/high",
            "priority-medium" => "priority/medium",
            "priority-low" => "priority/low",
            "scope-documentation" => "type/docs",
            "scope-enhancement" => "type/chore",
            "scope-bug" => "type/bug",
            "scope-performance" => "type/performance",
            "scope-new-feature" => "type/feature",
            "scope-security" => "type/security",
            "status-in-progress" => "status/in-progress",
            "status-duplicate" => "status/duplicate",
            "status-stale" => "status/stale",
            "status-incomplete" => "status/incomplete",
            "status-invalid" => "status/invalid",
            "status-wontfix" => "status/wontfix",
            _ => name
        };
    }
}
