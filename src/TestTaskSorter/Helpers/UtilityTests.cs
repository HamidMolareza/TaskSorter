using System.IO.Abstractions.TestingHelpers;
using TaskSorter.Helpers;

namespace TestTaskSorter.Helpers;

public class UtilityTests {
    private readonly MockFileSystem _fileSystem = new();
    private const string ExistFileName = "file.txt";

    public UtilityTests() {
        _fileSystem.AddFile(ExistFileName, "");
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(null)]
    public void IsFileExist_GiveNullOrEmptyInput_ReturnsFalse(string? path) {
        // Act
        var actual = _fileSystem.IsFileExist(path);

        // Assert
        Assert.False(actual);
    }

    [Fact]
    public void IsFileExist_GiveExistFile_ReturnsTrue() {
        // Act
        var actual = _fileSystem.IsFileExist(ExistFileName);

        // Assert
        Assert.True(actual);
    }

    [Fact]
    public void IsFileExist_GiveNOnExistFile_ReturnsFalse() {
        // Act
        var actual = _fileSystem.IsFileExist("not-exist");

        // Assert
        Assert.False(actual);
    }
}