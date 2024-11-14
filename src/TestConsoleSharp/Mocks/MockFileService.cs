using ConsoleSharpTemplate.Helpers.FileService;

namespace TestConsoleSharp.Mocks;

public class MockFileService : IFileService {
    public bool Exists(string path) =>
        path.StartsWith("exist-file", StringComparison.CurrentCultureIgnoreCase);
}