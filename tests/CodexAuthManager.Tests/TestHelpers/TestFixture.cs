using System;
using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Infrastructure;
using CodexAuthManager.Core.Services;
using System.IO;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// Test fixture providing common test dependencies
/// </summary>
public class TestFixture : IDisposable
{
    private readonly string _rootPath;

    public IFileSystem FileSystem { get; }
    public IPathProvider PathProvider { get; }
    public TokenDatabase Database { get; }
    public IIdentityRepository IdentityRepository { get; }
    public ITokenVersionRepository TokenVersionRepository { get; }
    public JwtDecoderService JwtDecoder { get; }
    public TokenManagementService TokenManagement { get; }
    public AuthJsonService AuthJsonService { get; }
    public DatabaseBackupService BackupService { get; }
    public IUsageStatsRepository UsageStatsRepository { get; }
    public CodexTuiService CodexTuiService { get; }

    public TestFixture()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "CodexAuthManagerTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        // Set up real file system bound to a temporary root
        FileSystem = new RealFileSystem();
        PathProvider = new TestPathProvider(_rootPath);

        // Ensure base folders exist
        FileSystem.EnsureDirectoryExists(PathProvider.GetDatabasePath());
        Directory.CreateDirectory(PathProvider.GetBackupFolderPath());
        Directory.CreateDirectory(Path.GetDirectoryName(PathProvider.GetActiveAuthJsonPath())!);

        // Set up database using on-disk storage so backups operate on the real file
        Database = new TokenDatabase(PathProvider, useInMemory: false);
        Database.InitializeAsync().Wait();

        // Set up repositories
        IdentityRepository = new IdentityRepository(Database);
        TokenVersionRepository = new TokenVersionRepository(Database);
        UsageStatsRepository = new UsageStatsRepository(Database);

        // Set up services
        JwtDecoder = new JwtDecoderService();
        TokenManagement = new TokenManagementService(IdentityRepository, TokenVersionRepository, JwtDecoder);
        AuthJsonService = new AuthJsonService(FileSystem, PathProvider);
        BackupService = new DatabaseBackupService(PathProvider, FileSystem);

        var mockProcessRunner = new MockCodexProcessRunner();
        CodexTuiService = new CodexTuiService(FileSystem, PathProvider, AuthJsonService, mockProcessRunner);
    }

    public void Dispose()
    {
        Database?.Dispose();
        if (Directory.Exists(_rootPath))
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup issues to avoid masking test failures
            }
        }
    }
}
