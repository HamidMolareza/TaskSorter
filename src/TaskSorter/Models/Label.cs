using System.IO.Abstractions;
using OnRails;
using OnRails.Extensions.OnFail;
using OnRails.Extensions.OnSuccess;
using OnRails.Extensions.Try;
using TaskSorter.Helpers;

namespace TaskSorter.Models;

public class Label(string name, int? value = null) : IEquatable<Label> {
    public string Name { get; init; } = NormalizeName(name);
    public string DisplayName { get; init; } = name;
    public int? Value { get; init; } = value;

    public static Task<Result<List<Label>>> ReadFileAsync(IFileSystem fileSystem, string file) =>
        TryExtensions.Try(() => fileSystem.File.ReadAllLinesAsync(file))
            .OnSuccess(Utility.CleanLines)
            .OnSuccess(lines => lines.Where(line => !line.StartsWith('#')).ToList())
            .OnSuccess(lines => {
                var maximumValue = lines.Count + 1;

                return lines
                    .Select((line, index) => new Label(line, maximumValue - index))
                    .ToList();
            }).OnFailAddMoreDetails(new { file });

    // Override Equals(object)
    public override bool Equals(object? obj) {
        return Equals(obj as Label);
    }

    // Implement IEquatable<LabelData>
    public bool Equals(Label? other) {
        if (other is null)
            return false;

        // Assuming equality is based on Name and Value
        return Name == other.Name;
    }

    // Override GetHashCode
    public override int GetHashCode() {
        // Use a hash code combination of Name and Value
        return HashCode.Combine(Name);
    }

    // Optionally, override the equality operators
    public static bool operator ==(Label? left, Label? right) {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(Label? left, Label? right) {
        return !(left == right);
    }

    private static string NormalizeName(string value) {
        var name = value.Trim().ToLowerInvariant();

        return name switch {
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
