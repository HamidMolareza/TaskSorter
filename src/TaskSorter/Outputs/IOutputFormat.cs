using TaskSorter.Tasks;

namespace TaskSorter.Outputs;

public interface IOutputFormat {
    public string FileExtension { get; }
    public string ToStr(List<TaskData> tasks);
}