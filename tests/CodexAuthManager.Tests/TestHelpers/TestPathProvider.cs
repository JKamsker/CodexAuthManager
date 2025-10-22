using CodexAuthManager.Core.Abstractions;
using System.IO;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// In-memory path provider for testing
/// </summary>
public class TestPathProvider : IPathProvider
{
    private readonly string _basePath;

    public TestPathProvider(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public string GetCodexFolderPath() => Path.Combine(_basePath, ".codex");
    public string GetDatabasePath() => Path.Combine(_basePath, "tokens.db");
    public string GetBackupFolderPath() => Path.Combine(_basePath, "backups");
    public string GetActiveAuthJsonPath() => Path.Combine(GetCodexFolderPath(), "auth.json");
}
