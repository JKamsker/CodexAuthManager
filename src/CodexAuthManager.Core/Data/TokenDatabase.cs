using Microsoft.Data.Sqlite;
using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Core.Data;

/// <summary>
/// SQLite database context for token management
/// </summary>
public class TokenDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IPathProvider _pathProvider;
    private bool _initialized;

    public TokenDatabase(IPathProvider pathProvider, bool useInMemory = false)
    {
        _pathProvider = pathProvider;
        var connectionString = useInMemory
            ? "Data Source=:memory:"
            : $"Data Source={_pathProvider.GetDatabasePath()}";

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    public SqliteConnection Connection => _connection;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            -- Identities table
            CREATE TABLE IF NOT EXISTS Identities (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                PlanType TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                UNIQUE(Email, AccountId)
            );

            -- TokenVersions table
            CREATE TABLE IF NOT EXISTS TokenVersions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                IdentityId INTEGER NOT NULL,
                VersionNumber INTEGER NOT NULL,
                IdToken TEXT NOT NULL,
                AccessToken TEXT NOT NULL,
                RefreshToken TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                OpenAiApiKey TEXT,
                LastRefresh TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsCurrent INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (IdentityId) REFERENCES Identities(Id) ON DELETE CASCADE,
                UNIQUE(IdentityId, VersionNumber)
            );

            -- Indexes
            CREATE INDEX IF NOT EXISTS IX_Identities_Email ON Identities(Email);
            CREATE INDEX IF NOT EXISTS IX_Identities_IsActive ON Identities(IsActive);
            CREATE INDEX IF NOT EXISTS IX_TokenVersions_IdentityId ON TokenVersions(IdentityId);
            CREATE INDEX IF NOT EXISTS IX_TokenVersions_IsCurrent ON TokenVersions(IsCurrent);
        ";

        await command.ExecuteNonQueryAsync();
        _initialized = true;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
