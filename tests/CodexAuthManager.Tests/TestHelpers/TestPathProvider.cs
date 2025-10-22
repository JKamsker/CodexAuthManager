using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// In-memory path provider for testing
/// </summary>
public class TestPathProvider : IPathProvider
{
    private readonly string _basePath;

    public TestPathProvider(string basePath = "/test")
    {
        _basePath = basePath;
    }

    public string GetCodexFolderPath() => $"{_basePath}/.codex";
    public string GetDatabasePath() => $"{_basePath}/tokens.db";
    public string GetBackupFolderPath() => $"{_basePath}/backups";
    public string GetActiveAuthJsonPath() => $"{_basePath}/.codex/auth.json";
}
