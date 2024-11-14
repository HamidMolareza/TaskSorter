namespace ConsoleSharpTemplate.Helpers.FileService;

public class FileService : IFileService {
    public bool Exists(string path) => File.Exists(path);
}