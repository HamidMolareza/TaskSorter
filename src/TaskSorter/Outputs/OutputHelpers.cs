using TaskSorter.Helpers;
using TaskSorter.Tasks;

namespace TaskSorter.Outputs;

public static class OutputHelpers {
    public static async Task<string> SaveAsync(List<TaskData> tasks, OutputTypes outputType,
        string outputDir) {
        IOutputFormat formater = outputType switch {
            OutputTypes.Json => new JsonOutput(),
            OutputTypes.Text => new TextOutput(),
            OutputTypes.Markdown => new MarkdownOutput(),
            _ => throw new ArgumentOutOfRangeException(nameof(outputType), outputType, null)
        };

        if (string.IsNullOrWhiteSpace(outputDir))
            outputDir = string.Empty;
        else if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var filename = $"{DateTime.Now.GenerateFileName()}.{formater.FileExtension}";
        var filePath = Path.Combine(outputDir, filename);

        await File.WriteAllTextAsync(filePath, formater.ToStr(tasks));

        return filePath;
    }
}