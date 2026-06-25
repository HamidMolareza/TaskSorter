using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using OnRails.ResultDetails.Errors.BadRequest;
using TaskSorter;
using TaskSorter.Settings;
using TaskSorter.Tasks;

namespace TestTaskSorter;

public class AppTests {
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

        var taskServiceLoggerMock = new Mock<ILogger<TasksService>>();
        var tasksService = new TasksService(taskServiceLoggerMock.Object, settings);
        var app = new App(mockLogger.Object, settings, fileSystem, tasksService);

        // Act
        var result = await app.RunAsync();

        //Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Detail);
        Assert.IsType<ValidationError>(result.Detail);

        var errorDetail = result.Detail as ValidationError;
        Assert.Single(errorDetail!.Errors);
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

        var taskServiceLoggerMock = new Mock<ILogger<TasksService>>();
        var tasksService = new TasksService(taskServiceLoggerMock.Object, settings);
        var app = new App(mockLogger.Object, settings, fileSystem, tasksService);

        // Act
        var result = await app.RunAsync();

        //Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Detail);
        Assert.IsType<ValidationError>(result.Detail);

        var errorDetail = result.Detail as ValidationError;
        Assert.Single(errorDetail!.Errors);
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