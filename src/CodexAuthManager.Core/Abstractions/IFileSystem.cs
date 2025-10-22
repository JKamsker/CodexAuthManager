namespace CodexAuthManager.Core.Abstractions;

/// <summary>
/// Abstraction for file system operations to allow swapping between real and in-memory implementations
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
    string GetDirectoryPath(string path);
    void EnsureDirectoryExists(string path);
}
