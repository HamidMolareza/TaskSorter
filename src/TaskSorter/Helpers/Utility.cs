using System.IO.Abstractions;

namespace TaskSorter.Helpers;

public static class Utility {
    public static bool IsFileExist(this IFileSystem fileSystem, string? path) =>
        !string.IsNullOrWhiteSpace(path) && fileSystem.File.Exists(path);
}