using ConsoleSharpTemplate;
using TestConsoleSharp.Mocks;

namespace TestConsoleSharp;

public class CommandLineTests {
    private readonly AppSettings _settings = new();
    private readonly MockFileService _fileSystem = new();

    [Fact]
    public async Task InvokeAsync_WithValidFiles_ReturnsSuccessCode() {
        // Arrange
        const string repoFilePath = "exist-file-repo.txt";
        const string labelsFilePath = "exist-file-label.txt";
        var args = new[] { "-r", repoFilePath, "-l", labelsFilePath };

        // Act
        var result = await CommandLine.InvokeAsync(args, _settings, _fileSystem);

        // Assert
        Assert.Equal(0, result); // Assuming success returns 0
        Assert.Equal(repoFilePath, _settings.RepositoryFile);
        Assert.Equal(labelsFilePath, _settings.LabelsFile);
    }

    [Fact]
    public async Task InvokeAsync_MissingRepoFile_ReturnsError() {
        // Arrange
        const string repoFilePath = "";
        const string labelsFilePath = "exist-file-label.txt";
        var args = new[] { "-r", repoFilePath, "-l", labelsFilePath };

        // Act
        var result = await CommandLine.InvokeAsync(args, _settings, _fileSystem);

        // Assert
        Assert.NotEqual(0, result); // Assuming success returns 0
    }

    [Fact]
    public async Task InvokeAsync_InvalidRepoFile_ReturnsError() {
        // Arrange
        const string repoFilePath = "not-exist-file-repo.txt";
        const string labelsFilePath = "exist-file-label.txt";
        var args = new[] { "-r", repoFilePath, "-l", labelsFilePath };

        // Act
        var result = await CommandLine.InvokeAsync(args, _settings, _fileSystem);

        // Assert
        Assert.NotEqual(0, result); // Assuming success returns 0
    }

    [Fact]
    public async Task InvokeAsync_MissingLabelsFile_ReturnsError() {
        // Arrange
        const string repoFilePath = "exist-file-repo.txt";
        const string labelsFilePath = "";
        var args = new[] { "-r", repoFilePath, "-l", labelsFilePath };

        // Act
        var result = await CommandLine.InvokeAsync(args, _settings, _fileSystem);

        // Assert
        Assert.NotEqual(0, result); // Assuming success returns 0
    }

    [Fact]
    public async Task InvokeAsync_InvalidLabelsFile_ReturnsError() {
        // Arrange
        const string repoFilePath = "exist-file-repo.txt";
        const string labelsFilePath = "not-exist-file";
        var args = new[] { "-r", repoFilePath, "-l", labelsFilePath };

        // Act
        var result = await CommandLine.InvokeAsync(args, _settings, _fileSystem);

        // Assert
        Assert.NotEqual(0, result); // Assuming success returns 0
    }
}