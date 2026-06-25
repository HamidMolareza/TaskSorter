using TaskSorter.Core.Configuration;

namespace TaskSorter.Tests.Core;

public sealed class ProfileConfigurationParserTests
{
    private readonly ProfileConfigurationParser _parser = new();

    [Fact]
    public void Preview_GivenCurrentTextFormats_ReturnsRepositoriesAndLabels()
    {
        var preview = _parser.Preview(new ProfileConfiguration(
            """
            # comment
            Owner/Repo core
            Owner/Other maintenance
            """,
            """
            priority-high
            status/next
            """,
            10,
            500));

        Assert.True(preview.IsValid);
        Assert.Equal(2, preview.Repositories.Count);
        Assert.Equal("owner/repo", preview.Repositories[0].ToString());
        Assert.Equal("core", preview.Repositories[0].Tier.Name);
        Assert.Equal("priority/high", preview.Labels[0].Name);
    }

    [Fact]
    public void Preview_GivenInvalidRepositoryAndTaskLimit_ReturnsValidationErrors()
    {
        var preview = _parser.Preview(new ProfileConfiguration(
            "not-a-repository",
            "priority/high",
            0,
            -1));

        Assert.False(preview.IsValid);
        Assert.Contains(preview.Errors, error => error.Field == "repositoryLines[0]");
        Assert.Contains(preview.Errors, error => error.Field == "taskLimit");
        Assert.Contains(preview.Errors, error => error.Field == "delayInMilliseconds");
    }
}
