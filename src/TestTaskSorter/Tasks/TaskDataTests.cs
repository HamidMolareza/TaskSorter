using TaskSorter.Models;
using TaskSorter.Tasks;

namespace TestTaskSorter.Tasks;

public class TaskDataTests {
    [Fact]
    public void CalculateValue_GiveTieredProjectAndDailyLabels_ReturnsScoreComponents() {
        var task = CreateTask(
            new Repository("owner", "repo"),
            [
                new Label("priority/high"),
                new Label("status/next"),
                new Label("size/s"),
                new Label("external")
            ],
            assigned: true);

        var repositories = new List<Repository> {
            new("owner", "repo", 2, ProjectTier.Parse("core"))
        };
        var labels = new List<Label> {
            new("priority/critical", 5),
            new("priority/high", 4),
            new("status/next", 3),
            new("size/s", 2)
        };

        var value = task.CalculateValue(repositories, labels);

        Assert.Equal(611, value);
        Assert.Equal(502, task.RepositoryScore);
        Assert.Equal(9, task.LabelScore);
        Assert.Equal(50, task.StatusScore);
        Assert.Equal(30, task.SizeScore);
        Assert.Equal(20, task.AssignmentScore);
        Assert.Equal("core", task.ProjectTier);
        Assert.True(task.HasPriorityLabel);
        Assert.Equal("next", task.Status);
        Assert.Equal("s", task.Size);
        Assert.Single(task.UnscoredLabels);
    }

    [Fact]
    public void CalculateValue_GiveLegacyLabelNames_MatchesSlashPriorityLabels() {
        var task = CreateTask(
            new Repository("owner", "repo"),
            [
                new Label("priority-high"),
                new Label("scope-bug"),
                new Label("status-in-progress")
            ]);

        var repositories = new List<Repository> {
            new("owner", "repo", 1, ProjectTier.Parse("active"))
        };
        var labels = new List<Label> {
            new("priority/high", 10),
            new("type/bug", 5),
            new("status/in-progress", 3)
        };

        task.CalculateValue(repositories, labels);

        Assert.Equal(18, task.LabelScore);
        Assert.Empty(task.UnscoredLabels);
        Assert.Equal("in-progress", task.Status);
    }

    [Fact]
    public void Sort_GiveSameScoreTasks_PutsAvailableAssignedTaskFirst() {
        var assigned = CreateTask(new Repository("owner", "repo"), [], assigned: true);
        var unassigned = CreateTask(new Repository("owner", "repo"), [], assigned: false);
        assigned.Value = 100;
        unassigned.Value = 100;

        var sorted = TasksService.Sort([unassigned, assigned]);

        Assert.Same(assigned, sorted[0]);
    }

    private static TaskData CreateTask(Repository repository, List<Label> labels, bool assigned = false) =>
        new() {
            Id = 1,
            Title = "Task title",
            Type = TaskTypes.Issue,
            Repository = repository,
            Labels = labels,
            Url = "https://github.com/owner/repo/issues/1",
            Assigned = assigned,
            Locked = false,
            CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z")
        };
}
