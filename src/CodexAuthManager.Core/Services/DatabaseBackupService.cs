using System.IO;
using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Core.Services;

/// <summary>
/// Service for creating and managing database backups
/// </summary>
public class DatabaseBackupService
{
    private readonly IPathProvider _pathProvider;
    private readonly IFileSystem _fileSystem;

    public DatabaseBackupService(IPathProvider pathProvider, IFileSystem fileSystem)
    {
        _pathProvider = pathProvider;
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Creates a backup of the database with a timestamp
    /// </summary>
    public async Task<string> CreateBackupAsync()
    {
        var dbPath = _pathProvider.GetDatabasePath();
        if (!_fileSystem.FileExists(dbPath))
        {
            throw new FileNotFoundException("Database file not found", dbPath);
        }

        var backupFolder = _pathProvider.GetBackupFolderPath();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupFileName = $"tokens-backup-{timestamp}.db";
        var backupPath = Path.Combine(backupFolder, backupFileName);

        // Ensure the destination folder exists before writing the backup file
        _fileSystem.EnsureDirectoryExists(backupPath);

        // Use file streams with shared read access so the live SQLite connection
        // can keep the database open while we copy its bytes.
        await using (var sourceStream = new FileStream(
                         dbPath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.ReadWrite | FileShare.Delete))
        await using (var destinationStream = new FileStream(
                         backupPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None))
        {
            await sourceStream.CopyToAsync(destinationStream);
            await destinationStream.FlushAsync();
        }

        // Clean old backups (keep last 10)
        await CleanOldBackupsAsync();

        return backupPath;
    }

    /// <summary>
    /// Removes old backups, keeping only the most recent ones
    /// </summary>
    private async Task CleanOldBackupsAsync(int keepCount = 10)
    {
        await Task.Run(() =>
        {
            var backupFolder = _pathProvider.GetBackupFolderPath();
            var backupFiles = _fileSystem.EnumerateFiles(backupFolder, "tokens-backup-*.db")
                .OrderByDescending(f => f)
                .ToList();

            if (backupFiles.Count <= keepCount)
                return;

            var filesToDelete = backupFiles.Skip(keepCount);
            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file); // Using File.Delete directly since IFileSystem doesn't have delete
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
        });
    }

    /// <summary>
    /// Lists all available backups
    /// </summary>
    public IEnumerable<string> ListBackups()
    {
        var backupFolder = _pathProvider.GetBackupFolderPath();
        return _fileSystem.EnumerateFiles(backupFolder, "tokens-backup-*.db")
            .OrderByDescending(f => f);
    }
}
