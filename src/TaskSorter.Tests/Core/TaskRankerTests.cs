using TaskSorter.Core.Configuration;
using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Tests.Core;

public sealed class TaskRankerTests
{
    [Fact]
    public void Rank_GivenTieredProjectAndDailyLabels_ReturnsScoreComponents()
    {
        var task = CreateTask(
            new Repository("owner", "repo"),
            [
                new Label("priority/high"),
                new Label("status/next"),
                new Label("size/s"),
                new Label("external")
            ],
            assigned: true);

        var ranker = new TaskRanker();
        var result = ranker.Rank(
            [task],
            [new Repository("owner", "repo", 2, ProjectTier.Parse("core"))],
            [
                new Label("priority/critical", 5),
                new Label("priority/high", 4),
                new Label("status/next", 3),
                new Label("size/s", 2)
            ],
            10);

        var rankedTask = Assert.Single(result);
        Assert.Equal(531, rankedTask.Value);
        Assert.Equal(502, rankedTask.RepositoryScore);
        Assert.Equal(9, rankedTask.LabelScore);
        Assert.Equal(20, rankedTask.AssignmentScore);
        Assert.Equal("core", rankedTask.ProjectTier);
        Assert.Single(rankedTask.UnscoredLabels);
    }

    [Fact]
    public void Rank_GivenSameScoreTasks_PutsAvailableAssignedTaskFirst()
    {
        var assigned = CreateTask(new Repository("owner", "repo"), [], assigned: true);
        var unassigned = CreateTask(new Repository("owner", "repo"), [], assigned: false);
        var ranker = new TaskRanker();

        var result = ranker.Rank(
            [unassigned, assigned],
            [new Repository("owner", "repo")],
            [],
            10);

        Assert.Same(assigned, result[0]);
    }

    [Fact]
    public void Rank_GivenCustomPriorityFactors_UsesConfiguredAssignmentBonus()
    {
        var task = CreateTask(
            new Repository("owner", "repo"),
            [
                new Label("priority/high"),
                new Label("status/next"),
                new Label("size/s")
            ],
            assigned: true);

        var ranker = new TaskRanker();
        var result = ranker.Rank(
            [task],
            [new Repository("owner", "repo", 2, ProjectTier.Parse("core"))],
            [
                new Label("priority/high", 4),
                new Label("status/next", 3),
                new Label("size/s", 2)
            ],
            10,
            new TaskPriorityFactors { AssignmentBonus = 99 });

        var rankedTask = Assert.Single(result);
        Assert.Equal(610, rankedTask.Value);
        Assert.Equal(99, rankedTask.AssignmentScore);
    }

    [Fact]
    public void Rank_GivenStatusAndSizeLabels_UsesOnlyConfiguredLabelPriority()
    {
        var task = CreateTask(
            new Repository("owner", "repo"),
            [
                new Label("status/next"),
                new Label("size/s")
            ]);

        var ranker = new TaskRanker();
        var result = ranker.Rank(
            [task],
            [],
            [
                new Label("status/next", 7),
                new Label("size/s", 2)
            ],
            10);

        var rankedTask = Assert.Single(result);
        Assert.Equal(9, rankedTask.Value);
        Assert.Equal(9, rankedTask.LabelScore);
        Assert.Equal(0, rankedTask.RepositoryScore);
        Assert.Equal(0, rankedTask.AssignmentScore);
        Assert.Equal(0, rankedTask.LockScore);
    }

    [Fact]
    public void TaskPriorityFactors_FromJson_IgnoresLegacyStatusAndSizeFields()
    {
        var factors = TaskPriorityFactors.FromJson(
            """
            {
              "status": { "next": 99 },
              "size": { "small": 88 },
              "assignmentBonus": 44,
              "lockPenalty": -55
            }
            """);

        Assert.Equal(44, factors.AssignmentBonus);
        Assert.Equal(-55, factors.LockPenalty);
    }

    private static TaskData CreateTask(Repository repository, List<Label> labels, bool assigned = false) =>
        new()
        {
            Id = Random.Shared.NextInt64(),
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
