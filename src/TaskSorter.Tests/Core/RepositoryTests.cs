using TaskSorter.Core.Models;

namespace TaskSorter.Tests.Core;

public sealed class RepositoryTests
{
    [Fact]
    public void Parse_GivenRepositoryWithoutTier_DefaultsToActive()
    {
        var repository = Repository.Parse("Owner/Repo", 5);

        Assert.NotNull(repository);
        Assert.Equal("owner/repo", repository.ToString());
        Assert.Equal("active", repository.Tier.Name);
        Assert.Equal(305, repository.PriorityScore);
    }

    [Fact]
    public void Parse_GivenInvalidTier_ReturnsNull()
    {
        var repository = Repository.Parse("owner/repo urgent", 5);

        Assert.Null(repository);
    }
}
