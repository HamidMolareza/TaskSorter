using System.IO.Abstractions.TestingHelpers;
using TaskSorter.Models;

namespace TestTaskSorter.Models;

public class RepositoryTests {
    [Fact]
    public void Parse_GiveRepositoryWithoutTier_DefaultsToActive() {
        var repository = Repository.Parse("owner/repo", 5);

        Assert.NotNull(repository);
        Assert.Equal("owner/repo", repository.ToString());
        Assert.Equal("active", repository.Tier.Name);
        Assert.Equal(305, repository.PriorityScore);
    }

    [Fact]
    public void Parse_GiveRepositoryWithTier_ReturnsTieredRepository() {
        var repository = Repository.Parse("owner/repo core", 5);

        Assert.NotNull(repository);
        Assert.Equal("core", repository.Tier.Name);
        Assert.Equal(505, repository.PriorityScore);
    }

    [Fact]
    public void Parse_GiveInvalidTier_ReturnsNull() {
        var repository = Repository.Parse("owner/repo urgent", 5);

        Assert.Null(repository);
    }

    [Fact]
    public async Task ReadFileAsync_GiveCommentsAndTieredRepositories_ReturnsRepositories() {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> {
            ["repos.txt"] = new("""
                # Highest value first
                owner/core core
                owner/active
                """)
        });

        var result = await Repository.ReadFileAsync(fileSystem, "repos.txt");

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("core", result.Value[0].Tier.Name);
        Assert.Equal("active", result.Value[1].Tier.Name);
    }
}
