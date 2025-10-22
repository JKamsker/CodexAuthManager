using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Core.Infrastructure;

/// <summary>
/// Development path provider using separate paths to avoid interfering with production data
/// </summary>
public class DevelopmentPathProvider : IPathProvider
{
    private readonly string _basePath;

    public DevelopmentPathProvider(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexManager-Dev");

        Directory.CreateDirectory(_basePath);
    }

    public string GetCodexFolderPath()
    {
        var codexPath = Path.Combine(_basePath, ".codex");
        Directory.CreateDirectory(codexPath);
        return codexPath;
    }

    public string GetDatabasePath()
    {
        return Path.Combine(_basePath, "tokens-dev.db");
    }

    public string GetBackupFolderPath()
    {
        var backupFolder = Path.Combine(_basePath, "backups");
        Directory.CreateDirectory(backupFolder);
        return backupFolder;
    }

    public string GetActiveAuthJsonPath()
    {
        return Path.Combine(GetCodexFolderPath(), "auth.json");
    }
}
