using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Core.Infrastructure;

/// <summary>
/// Production path provider using actual system paths
/// </summary>
public class ProductionPathProvider : IPathProvider
{
    public string GetCodexFolderPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".codex");
    }

    public string GetDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbFolder = Path.Combine(appData, "CodexManager");
        Directory.CreateDirectory(dbFolder);
        return Path.Combine(dbFolder, "tokens.db");
    }

    public string GetBackupFolderPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var backupFolder = Path.Combine(appData, "CodexManager", "backups");
        Directory.CreateDirectory(backupFolder);
        return backupFolder;
    }

    public string GetActiveAuthJsonPath()
    {
        return Path.Combine(GetCodexFolderPath(), "auth.json");
    }
}
