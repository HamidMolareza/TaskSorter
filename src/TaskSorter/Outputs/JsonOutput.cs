using System.Text.Json;
using TaskSorter.Tasks;

namespace TaskSorter.Outputs;

public class JsonOutput : IOutputFormat {
    public string ToStr(List<TaskData> tasks) => JsonSerializer.Serialize(tasks, new JsonSerializerOptions {
        WriteIndented = true
    });

    public string FileExtension => "json";
}
