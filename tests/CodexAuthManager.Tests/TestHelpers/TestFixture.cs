using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Infrastructure;
using CodexAuthManager.Core.Services;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// Test fixture providing common test dependencies
/// </summary>
public class TestFixture : IDisposable
{
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
        // Set up in-memory file system
        FileSystem = new InMemoryFileSystem();
        PathProvider = new TestPathProvider();

        // Create fake database file for backup service
        var dbPath = PathProvider.GetDatabasePath();
        FileSystem.WriteAllText(dbPath, "fake-db-content");

        // Set up in-memory database
        Database = new TokenDatabase(PathProvider, useInMemory: true);
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
    }
}
