using CodexAuthManager.Core.Infrastructure;
using CodexAuthManager.Core.Services;
using CodexAuthManager.Tests.TestHelpers;
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
        // Arrange - Create a "fake" database file
        var dbPath = _fixture.PathProvider.GetDatabasePath();
        _fixture.FileSystem.WriteAllText(dbPath, "fake db content");

        // Act
        var backupPath = await _fixture.BackupService.CreateBackupAsync();

        // Assert
        Assert.NotNull(backupPath);
        Assert.True(_fixture.FileSystem.FileExists(backupPath));
        Assert.Contains("tokens-backup-", backupPath);
        Assert.EndsWith(".db", backupPath);
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldThrow_WhenDatabaseNotFound()
    {
        // Arrange - Create a new fixture without the fake database file
        using var tempFixture = new TestFixture();
        var tempPathProvider = new TestPathProvider("/temp");
        var tempFileSystem = new InMemoryFileSystem();
        var tempBackupService = new DatabaseBackupService(tempPathProvider, tempFileSystem);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await tempBackupService.CreateBackupAsync());
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCopyDatabaseContent()
    {
        // Arrange
        var dbPath = _fixture.PathProvider.GetDatabasePath();
        var originalContent = "test database content";
        _fixture.FileSystem.WriteAllText(dbPath, originalContent);

        // Act
        var backupPath = await _fixture.BackupService.CreateBackupAsync();

        // Assert
        var backupContent = _fixture.FileSystem.ReadAllText(backupPath);
        Assert.Equal(originalContent, backupContent);
    }

    [Fact]
    public void ListBackups_ShouldReturnAllBackupFiles()
    {
        // Arrange
        var backupFolder = _fixture.PathProvider.GetBackupFolderPath();
        _fixture.FileSystem.WriteAllText($"{backupFolder}/tokens-backup-20251022-120000.db", "backup1");
        _fixture.FileSystem.WriteAllText($"{backupFolder}/tokens-backup-20251022-130000.db", "backup2");
        _fixture.FileSystem.WriteAllText($"{backupFolder}/tokens-backup-20251022-140000.db", "backup3");
        _fixture.FileSystem.WriteAllText($"{backupFolder}/other-file.txt", "not a backup");

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
        _fixture.FileSystem.WriteAllText($"{backupFolder}/tokens-backup-20251022-100000.db", "backup1");
        _fixture.FileSystem.WriteAllText($"{backupFolder}/tokens-backup-20251022-200000.db", "backup3");
        _fixture.FileSystem.WriteAllText($"{backupFolder}/tokens-backup-20251022-150000.db", "backup2");

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
}
