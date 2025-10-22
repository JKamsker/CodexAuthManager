using CodexAuthManager.Core.Abstractions;
using System.Collections.Concurrent;

namespace CodexAuthManager.Core.Infrastructure;

/// <summary>
/// In-memory file system implementation for testing
/// </summary>
public class InMemoryFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, string> _files = new();
    private readonly ConcurrentDictionary<string, bool> _directories = new();

    public bool FileExists(string path) => _files.ContainsKey(NormalizePath(path));

    public string ReadAllText(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (!_files.TryGetValue(normalizedPath, out var content))
            throw new FileNotFoundException($"File not found: {path}");
        return content;
    }

    public void WriteAllText(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = content;

        // Ensure directory exists
        var directory = GetDirectoryPath(normalizedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            _directories[directory] = true;
        }
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
    {
        var normalizedPath = NormalizePath(path);
        var pattern = searchPattern.Replace("*", ".*").Replace("?", ".");
        var regex = new System.Text.RegularExpressions.Regex(pattern);

        return _files.Keys
            .Where(filePath =>
            {
                var directory = GetDirectoryPath(filePath);
                return directory == normalizedPath && regex.IsMatch(Path.GetFileName(filePath));
            });
    }

    public string GetDirectoryPath(string path)
    {
        var normalized = NormalizePath(path);
        var lastSeparator = normalized.LastIndexOfAny(new[] { '/', '\\' });
        return lastSeparator >= 0 ? normalized.Substring(0, lastSeparator) : string.Empty;
    }

    public void EnsureDirectoryExists(string path)
    {
        var directory = GetDirectoryPath(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _directories[directory] = true;
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }
}
