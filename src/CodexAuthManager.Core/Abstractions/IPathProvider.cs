namespace CodexAuthManager.Core.Abstractions;

/// <summary>
/// Provides paths for Codex folder and database location
/// </summary>
public interface IPathProvider
{
    string GetCodexFolderPath();
    string GetDatabasePath();
    string GetBackupFolderPath();
    string GetActiveAuthJsonPath();
}
