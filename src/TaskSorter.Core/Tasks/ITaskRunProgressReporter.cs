namespace TaskSorter.Core.Tasks;

public interface ITaskRunProgressReporter
{
    ValueTask ReportAsync(TaskRunProgress progress, CancellationToken cancellationToken);
}
