using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using OnRail.ResultDetails.Errors;
using TaskSorter;

namespace TestTaskSorter;

public class AppTests {
    [Fact]
    public async Task Run_GiveValidInputs_ReturnsOk() {
        // Arrange
        var mockLogger = new Mock<ILogger<App>>();
        var settings = AppTestsUtility.CreateSettings();
        var fileSystem = AppTestsUtility.CreateFileSystem([settings.RepositoryFile, settings.LabelsFile]);
        var app = new App(mockLogger.Object, settings, fileSystem);

        // Act
        var actual = await app.RunAsync();

        //Assert
        Assert.True(actual.IsSuccess);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("invalid-path")]
    public async Task Run_GiveInvalidRepoFile_ReturnsValidationError(string? repoFilePath) {
        // Arrange
        var settings = AppTestsUtility.CreateSettings();
        settings.RepositoryFile = repoFilePath!;

        var mockLogger = new Mock<ILogger<App>>();
        var fileSystem = AppTestsUtility.CreateFileSystem([]);
        var app = new App(mockLogger.Object, settings, fileSystem);

        // Act
        var result = await app.RunAsync();

        //Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Detail);
        Assert.IsType<ValidationError>(result.Detail);
        Assert.Contains($"{nameof(settings.RepositoryFile)} is required.", result.Detail.Message);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("invalid-path")]
    public async Task Run_GiveInvalidLabelFile_ReturnsValidationError(string? labelFilePath) {
        // Arrange
        var settings = AppTestsUtility.CreateSettings();
        settings.LabelsFile = labelFilePath!;

        var mockLogger = new Mock<ILogger<App>>();
        var fileSystem = AppTestsUtility.CreateFileSystem([settings.RepositoryFile]);
        var app = new App(mockLogger.Object, settings, fileSystem);

        // Act
        var result = await app.RunAsync();

        //Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Detail);
        Assert.IsType<ValidationError>(result.Detail);
        Assert.Contains($"{nameof(settings.LabelsFile)} is required.", result.Detail.Message);
    }
}

static class AppTestsUtility {
    public static AppSettings CreateSettings() => new() {
        LabelsFile = "label.txt",
        RepositoryFile = "repo.txt"
    };

    public static MockFileSystem CreateFileSystem(IEnumerable<string> existFiles) {
        var fileSystem = new MockFileSystem();
        foreach (var file in existFiles)
            fileSystem.AddFile(file, string.Empty);
        return fileSystem;
    }
}