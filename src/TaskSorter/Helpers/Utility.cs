using System.IO.Abstractions;
using TaskSorter.Models;

namespace TaskSorter.Helpers;

public static class Utility {
    public static bool IsFileExist(this IFileSystem fileSystem, string? path) =>
        !string.IsNullOrWhiteSpace(path) && fileSystem.File.Exists(path);

    /// <summary>
    /// Ignore empty (or whitespace) lines, then trim remain lines and return
    /// </summary>
    /// <param name="lines"></param>
    /// <returns></returns>
    public static List<string> CleanLines(IEnumerable<string> lines) =>
        lines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();

    public static string ToStr(this IEnumerable<Label> labels) =>
        string.Join(", ", labels.Select(l => l.DisplayName));

    public static string GenerateFileName(this DateTime dateTime) {
        return dateTime.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    public static string RemoveDomain(string url) {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        try {
            // Parse the URL
            var uri = new Uri(url);

            // Return the path and query string (e.g., "path/to/resource?query=1")
            return uri.PathAndQuery.TrimStart('/');
        }
        catch (UriFormatException) {
            throw new ArgumentException("Invalid URL format.", nameof(url));
        }
    }
}