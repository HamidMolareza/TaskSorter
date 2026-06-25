using System.IO.Abstractions;
using OnRails;
using OnRails.Extensions.OnFail;
using OnRails.Extensions.OnSuccess;
using OnRails.Extensions.Try;
using OnRails.ResultDetails.Errors.BadRequest;
using TaskSorter.Helpers;

namespace TaskSorter.Models;

public class Repository(string owner, string name, int? value = null, ProjectTier? tier = null) {
    public string Owner { get; } = owner.ToLower();
    public string Name { get; } = name.ToLower();
    public int? Value { get; } = value;
    public ProjectTier Tier { get; } = tier ?? ProjectTier.Active;
    public int PriorityScore => (Value ?? 0) + Tier.Score;
    public override string ToString() => $"{Owner}/{Name}";

    // Override Equals
    public override bool Equals(object? obj) {
        if (obj is not Repository other)
            return false;

        return Owner == other.Owner &&
               Name == other.Name;
    }

    // Override GetHashCode
    public override int GetHashCode() =>
        HashCode.Combine(Owner, Name);

    // Overload the == operator
    public static bool operator ==(Repository? left, Repository? right) {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    // Overload the != operator
    public static bool operator !=(Repository? left, Repository? right) =>
        !(left == right);

    public static Repository? Parse(string text, int value) {
        var parts = text.Trim()
            .Split([' ', '\t', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 2)
            return null;

        var tier = parts.Length == 2 ? ProjectTier.Parse(parts[1]) : ProjectTier.Active;
        if (tier is null)
            return null;

        var split = parts[0].Split("/");
        if (split.Length != 2)
            return null;

        var owner = split[0];
        var repoName = split[1];
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repoName))
            return null;

        return new Repository(owner, repoName, value, tier);
    }

    public static Task<Result<List<Repository>>> ReadFileAsync(IFileSystem fileSystem, string file) =>
        TryExtensions.Try(() => fileSystem.File.ReadAllLinesAsync(file))
            .OnSuccess(Utility.CleanLines)
            .OnSuccess(lines => lines.Where(line => !line.StartsWith('#')).ToList())
            .OnSuccess(lines => {
                var maximumValue = lines.Count + 1;

                var result = new List<Repository>();
                for (var i = 0; i < lines.Count; i++) {
                    var repository = Parse(lines[i], maximumValue - i);
                    if (repository is null) {
                        return Result<List<Repository>>.Fail(
                            new ValidationError(lines[i],
                                $"Repository name is not valid. Use 'owner/repo' or 'owner/repo tier'. Valid tiers: {ProjectTier.ValidNames}."));
                    }

                    result.Add(repository);
                }

                return Result<List<Repository>>.Ok(result);
            }).OnFailAddMoreDetails(new { RepoFilePath = file });
}
