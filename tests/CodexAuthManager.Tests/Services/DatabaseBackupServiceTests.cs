using System;
using CodexAuthManager.Core.Infrastructure;
using CodexAuthManager.Core.Services;
using CodexAuthManager.Tests.TestHelpers;
using System.IO;
using Xunit;

namespace CodexAuthManager.Tests.Services;

public class DatabaseBackupServiceTests : IDisposable
{
    private readonly TestFixture _fixture;

    public DatabaseBackupServiceTests()
    {
        _fixture = new TestFixture();
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCreateBackupFile()
    {
        // Arrange - ensure database has some data written
        await SeedSampleIdentityAsync();

        // Act
        var backupPath = await _fixture.BackupService.CreateBackupAsync();

        // Assert
        Assert.NotNull(backupPath);
        Assert.True(_fixture.FileSystem.FileExists(backupPath));
        Assert.Contains("tokens-backup-", backupPath);
        Assert.EndsWith(".db", backupPath);
        Assert.True(new FileInfo(backupPath).Length > 0);
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldThrow_WhenDatabaseNotFound()
    {
        // Arrange - point the backup service to a temp location with no database file
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "CodexAuthManagerTests",
            $"missing-db-{Guid.NewGuid():N}");
        var tempPathProvider = new TestPathProvider(tempRoot);
        var tempFileSystem = new RealFileSystem();
        var tempBackupService = new DatabaseBackupService(tempPathProvider, tempFileSystem);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await tempBackupService.CreateBackupAsync());

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCopyDatabaseContent()
    {
        // Arrange
        await SeedSampleIdentityAsync();
        var dbPath = _fixture.PathProvider.GetDatabasePath();

        // Act
        var backupPath = await _fixture.BackupService.CreateBackupAsync();

        // Assert
        var dbBytes = await ReadAllBytesSharedAsync(dbPath);
        var backupBytes = await File.ReadAllBytesAsync(backupPath);
        Assert.Equal(dbBytes, backupBytes);
        Assert.True(backupBytes.Length > 0);
    }

    [Fact]
    public void ListBackups_ShouldReturnAllBackupFiles()
    {
        // Arrange
        var backupFolder = _fixture.PathProvider.GetBackupFolderPath();
        _fixture.FileSystem.WriteAllText(Path.Combine(backupFolder, "tokens-backup-20251022-120000.db"), "backup1");
        _fixture.FileSystem.WriteAllText(Path.Combine(backupFolder, "tokens-backup-20251022-130000.db"), "backup2");
        _fixture.FileSystem.WriteAllText(Path.Combine(backupFolder, "tokens-backup-20251022-140000.db"), "backup3");
        _fixture.FileSystem.WriteAllText(Path.Combine(backupFolder, "other-file.txt"), "not a backup");

        // Act
        var backups = _fixture.BackupService.ListBackups().ToList();

        // Assert
        Assert.Equal(3, backups.Count);
        Assert.All(backups, b => Assert.Contains("tokens-backup-", b));
    }

    [Fact]
    public void ListBackups_ShouldReturnEmpty_WhenNoBackups()
    {
        // Act
        var backups = _fixture.BackupService.ListBackups().ToList();

        // Assert
        Assert.Empty(backups);
    }

    [Fact]
    public void ListBackups_ShouldReturnBackupsInDescendingOrder()
    {
        // Arrange
        var backupFolder = _fixture.PathProvider.GetBackupFolderPath();
        _fixture.FileSystem.WriteAllText(Path.Combine(backupFolder, "tokens-backup-20251022-100000.db"), "backup1");
        _fixture.FileSystem.WriteAllText(Path.Combine(backupFolder, "tokens-backup-20251022-200000.db"), "backup3");
        _fixture.FileSystem.WriteAllText(Path.Combine(backupFolder, "tokens-backup-20251022-150000.db"), "backup2");

        // Act
        var backups = _fixture.BackupService.ListBackups().ToList();

        // Assert
        Assert.Equal(3, backups.Count);
        Assert.Contains("200000", backups[0]); // Most recent first
        Assert.Contains("150000", backups[1]);
        Assert.Contains("100000", backups[2]);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }

    private async Task SeedSampleIdentityAsync()
    {
        var authToken = SampleData.CreateAuthToken();
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);
    }

    private static async Task<byte[]> ReadAllBytesSharedAsync(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
