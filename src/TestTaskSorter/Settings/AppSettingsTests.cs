using TaskSorter.Settings;

namespace TestTaskSorter.Settings;

using Xunit;

public class AppSettingsTests {
    [Fact]
    public void ToString_ShouldIncludePublicProperties_WithoutSecureConfig() {
        // Arrange
        var appSettings = new AppSettings {
            RepositoryFile = "repos.txt",
            LabelsFile = "labels.txt",
            GithubToken = "supersecrettoken",
            GithubTokenEnvName = "CUSTOM_GITHUB_TOKEN",
            OutputTypes = ["json", "console"],
            OutputDir = "./output",
            DelayInMilliSeconds = 500
        };

        // Act
        var result = appSettings.ToString();

        // Assert
        Assert.Contains("RepositoryFile: repos.txt", result);
        Assert.Contains("LabelsFile: labels.txt", result);
        Assert.Contains("GithubTokenEnvName: CUSTOM_GITHUB_TOKEN", result);
        Assert.Contains("OutputTypes: json, console", result);
        Assert.Contains("OutputDir: ./output", result);
        Assert.Contains("DelayInMilliSeconds: 500", result);

        // Ensure the SecureConfig property is excluded
        Assert.DoesNotContain("GithubToken: supersecrettoken", result);
    }

    [Fact]
    public void ToString_ShouldHandleEmptyCollections() {
        // Arrange
        var appSettings = new AppSettings {
            RepositoryFile = "repos.txt",
            LabelsFile = "labels.txt",
            OutputTypes = [],
            OutputDir = string.Empty,
            DelayInMilliSeconds = 0
        };

        // Act
        var result = appSettings.ToString();

        // Assert
        Assert.Contains("OutputTypes: ", result); // Empty list
        Assert.Contains("OutputDir: ", result); // Empty string
        Assert.Contains("DelayInMilliSeconds: 0", result);
    }

    [Fact]
    public void ToString_ShouldHandleNullValues() {
        // Arrange
        var appSettings = new AppSettings {
            RepositoryFile = null!,
            LabelsFile = null!,
            GithubTokenEnvName = null,
            OutputTypes = null!,
            OutputDir = null!,
            DelayInMilliSeconds = 0
        };

        // Act
        var result = appSettings.ToString();

        // Assert
        Assert.Contains("RepositoryFile: null", result);
        Assert.Contains("LabelsFile: null", result);
        Assert.Contains("GithubTokenEnvName: null", result);
        Assert.Contains("OutputTypes: null", result);
        Assert.Contains("OutputDir: null", result);
    }
}