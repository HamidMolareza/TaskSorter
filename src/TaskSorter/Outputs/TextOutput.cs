using System.Text;
using TaskSorter.Helpers;
using TaskSorter.Tasks;

namespace TaskSorter.Outputs;

public class TextOutput : IOutputFormat {
    public string ToStr(List<TaskData> tasks) {
        var sb = new StringBuilder();

        if (tasks.Count == 0)
            return "No open tasks matched the configured repositories.\n";

        sb.AppendLine("TaskSorter Daily Queue");
        foreach (var (task, index) in tasks.Select((task, index) => (task, index))) {
            sb.AppendLine()
                .AppendLine($"#{index + 1} | Score: {task.Value} | {task.Type} | {task.Title}")
                .AppendLine($"Project: {task.Repository} ({task.ProjectTier})")
                .AppendLine($"Status: {task.Status}")
                .AppendLine($"Size: {task.Size}")
                .AppendLine($"Labels: {task.Labels.ToStr()}")
                .AppendLine($"Assigned: {task.Assigned}")
                .AppendLine($"Locked: {task.Locked}")
                .AppendLine($"Url: {task.Url}")
                .AppendLine($"CreatedAt: {task.CreatedAt:yyyy-MM-dd HH:mm:ss}")
                .AppendLine($"UpdatedAt: {task.UpdatedAt:yyyy-MM-dd HH:mm:ss}")
                .AppendLine(
                    $"ScoreDetails: repository={task.RepositoryScore}, labels={task.LabelScore}, status={task.StatusScore}, size={task.SizeScore}, assigned={task.AssignmentScore}, locked={task.LockScore}");
        }

        return sb.ToString();
    }

    public string FileExtension => "txt";
}
