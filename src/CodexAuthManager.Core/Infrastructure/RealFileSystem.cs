using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Core.Infrastructure;

/// <summary>
/// Real file system implementation using System.IO
/// </summary>
public class RealFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void WriteAllText(string path, string content)
    {
        EnsureDirectoryExists(path);
        File.WriteAllText(path, content);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
    {
        if (!Directory.Exists(path))
            return Enumerable.Empty<string>();

        return Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
    }

    public string GetDirectoryPath(string path) => Path.GetDirectoryName(path) ?? string.Empty;

    public void EnsureDirectoryExists(string path)
    {
        var directory = GetDirectoryPath(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
