using System.Text;
using TaskSorter.Helpers;
using TaskSorter.Tasks;

namespace TaskSorter.Outputs;

public class MarkdownOutput : IOutputFormat {
    private const string BoldFormat = "**{0}**";

    public string ToStr(List<TaskData> tasks) {
        var sb = new StringBuilder();

        if (tasks.Count == 0)
            return "No open tasks matched the configured repositories.\n";

        sb.AppendLine("# TaskSorter Daily Queue")
            .AppendLine();

        sb.AppendLine(
                "| Rank | Score | Project | Tier | Status | Size | Type | Title | Labels | Assigned | URL | Updated At |")
            .AppendLine(
                "|------|-------|---------|------|--------|------|------|-------|--------|----------|-----|------------|");
        foreach (var (task, index) in tasks.Select((task, index) => (task, index))) {
            var urlLabel = Utility.RemoveDomain(task.Url);
            var assignedLabel = task.Assigned ? string.Format(BoldFormat, task.Assigned) : task.Assigned.ToString();

            sb.AppendLine(
                $"| {index + 1} | {task.Value} | {Escape(task.Repository.ToString())} | {task.ProjectTier} | {task.Status} | {task.Size} | {task.Type} | {Escape(task.Title)} | {Escape(task.Labels.ToStr())} | {assignedLabel} | [{Escape(urlLabel)}]({task.Url}) | {task.UpdatedAt:yyyy-MM-dd HH:mm:ss} |");
        }

        return sb.ToString();
    }

    public string FileExtension => "md";

    private static string Escape(string value) =>
        value.Replace("|", "\\|").ReplaceLineEndings(" ");
}
