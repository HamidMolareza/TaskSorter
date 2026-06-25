namespace TaskSorter.Core.Models;

public sealed class Repository(string owner, string name, int? value = null, ProjectTier? tier = null)
{
    public string Owner { get; } = owner.Trim().ToLowerInvariant();
    public string Name { get; } = name.Trim().ToLowerInvariant();
    public int? Value { get; } = value;
    public ProjectTier Tier { get; } = tier ?? ProjectTier.Active;
    public int PriorityScore => (Value ?? 0) + Tier.Score;

    public static Repository? Parse(string text, int value)
    {
        var parts = text.Trim()
            .Split([' ', '\t', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 2)
            return null;

        var tier = parts.Length == 2 ? ProjectTier.Parse(parts[1]) : ProjectTier.Active;
        if (tier is null)
            return null;

        var split = parts[0].Split('/');
        if (split.Length != 2)
            return null;

        if (string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]))
            return null;

        return new Repository(split[0], split[1], value, tier);
    }

    public override string ToString() => $"{Owner}/{Name}";

    public override bool Equals(object? obj) =>
        obj is Repository other && Owner == other.Owner && Name == other.Name;

    public override int GetHashCode() =>
        HashCode.Combine(Owner, Name);

    public static bool operator ==(Repository? left, Repository? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Repository? left, Repository? right) =>
        !(left == right);
}
