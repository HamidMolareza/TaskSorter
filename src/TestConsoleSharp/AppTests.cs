using System.IO.Abstractions.TestingHelpers;
using ConsoleSharpTemplate;
using Microsoft.Extensions.Logging;
using Moq;

namespace TestConsoleSharp;

public class AppTests {
    [Fact]
    public async Task Run_GiveValidInputs() {
        // Arrange
        var mockLogger = new Mock<ILogger<App>>();
        var settings = AppTestsUtility.CreateSettings();
        var fileSystem = AppTestsUtility.CreateFileSystem([settings.RepositoryFile, settings.LabelsFile]);
        var app = new App(mockLogger.Object, settings, fileSystem);

        // Act
        await app.RunAsync();
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("invalid-path")]
    public async Task Run_GiveInvalidRepoFile(string? repoFilePath) {
        // Arrange
        var settings = AppTestsUtility.CreateSettings();
        settings.RepositoryFile = repoFilePath!;

        var mockLogger = new Mock<ILogger<App>>();
        var fileSystem = AppTestsUtility.CreateFileSystem([]);
        var app = new App(mockLogger.Object, settings, fileSystem);

        // Act
        await app.RunAsync();

        //Assert
        mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("is required.") &&
                    v.ToString()!.Contains(nameof(settings.RepositoryFile))
                ),
                null,
                ((Func<It.IsAnyType, Exception, string>)It.IsAny<object>())!
            ),
            Times.Once);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("invalid-path")]
    public async Task Run_GiveInvalidLabelFile(string? labelFilePath) {
        // Arrange
        var settings = AppTestsUtility.CreateSettings();
        settings.LabelsFile = labelFilePath!;

        var mockLogger = new Mock<ILogger<App>>();
        var fileSystem = AppTestsUtility.CreateFileSystem([]);
        var app = new App(mockLogger.Object, settings, fileSystem);

        // Act
        await app.RunAsync();

        //Assert
        mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("is required.") &&
                    v.ToString()!.Contains(nameof(settings.LabelsFile))
                ),
                null,
                ((Func<It.IsAnyType, Exception, string>)It.IsAny<object>())!
            ),
            Times.Once);
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