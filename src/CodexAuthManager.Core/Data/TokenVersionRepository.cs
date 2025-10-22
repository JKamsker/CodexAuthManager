using CodexAuthManager.Core.Models;
using Microsoft.Data.Sqlite;

namespace CodexAuthManager.Core.Data;

/// <summary>
/// SQLite implementation of token version repository
/// </summary>
public class TokenVersionRepository : ITokenVersionRepository
{
    private readonly TokenDatabase _database;

    public TokenVersionRepository(TokenDatabase database)
    {
        _database = database;
    }

    public async Task<TokenVersion?> GetByIdAsync(int id)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, IdentityId, VersionNumber, IdToken, AccessToken, RefreshToken,
                   AccountId, OpenAiApiKey, LastRefresh, CreatedAt, IsCurrent
            FROM TokenVersions
            WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return reader.Read() ? ReadTokenVersion(reader) : null;
    }

    public async Task<TokenVersion?> GetCurrentVersionAsync(int identityId)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, IdentityId, VersionNumber, IdToken, AccessToken, RefreshToken,
                   AccountId, OpenAiApiKey, LastRefresh, CreatedAt, IsCurrent
            FROM TokenVersions
            WHERE IdentityId = @identityId AND IsCurrent = 1
            LIMIT 1";
        command.Parameters.AddWithValue("@identityId", identityId);

        await using var reader = await command.ExecuteReaderAsync();
        return reader.Read() ? ReadTokenVersion(reader) : null;
    }

    public async Task<IEnumerable<TokenVersion>> GetVersionsAsync(int identityId)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, IdentityId, VersionNumber, IdToken, AccessToken, RefreshToken,
                   AccountId, OpenAiApiKey, LastRefresh, CreatedAt, IsCurrent
            FROM TokenVersions
            WHERE IdentityId = @identityId
            ORDER BY VersionNumber DESC";
        command.Parameters.AddWithValue("@identityId", identityId);

        var versions = new List<TokenVersion>();
        await using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            versions.Add(ReadTokenVersion(reader));
        }
        return versions;
    }

    public async Task<int> CreateAsync(TokenVersion tokenVersion)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO TokenVersions (IdentityId, VersionNumber, IdToken, AccessToken, RefreshToken,
                                      AccountId, OpenAiApiKey, LastRefresh, CreatedAt, IsCurrent)
            VALUES (@identityId, @versionNumber, @idToken, @accessToken, @refreshToken,
                   @accountId, @openAiApiKey, @lastRefresh, @createdAt, @isCurrent);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@identityId", tokenVersion.IdentityId);
        command.Parameters.AddWithValue("@versionNumber", tokenVersion.VersionNumber);
        command.Parameters.AddWithValue("@idToken", tokenVersion.IdToken);
        command.Parameters.AddWithValue("@accessToken", tokenVersion.AccessToken);
        command.Parameters.AddWithValue("@refreshToken", tokenVersion.RefreshToken);
        command.Parameters.AddWithValue("@accountId", tokenVersion.AccountId);
        command.Parameters.AddWithValue("@openAiApiKey", (object?)tokenVersion.OpenAiApiKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastRefresh", tokenVersion.LastRefresh.ToString("O"));
        command.Parameters.AddWithValue("@createdAt", tokenVersion.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@isCurrent", tokenVersion.IsCurrent ? 1 : 0);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task SetCurrentVersionAsync(int identityId, int versionId)
    {
        await using var transaction = _database.Connection.BeginTransaction();
        try
        {
            // Set all versions to not current for this identity
            await using (var command = _database.Connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE TokenVersions SET IsCurrent = 0 WHERE IdentityId = @identityId";
                command.Parameters.AddWithValue("@identityId", identityId);
                await command.ExecuteNonQueryAsync();
            }

            // Set the specified version as current
            await using (var command = _database.Connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE TokenVersions SET IsCurrent = 1 WHERE Id = @id";
                command.Parameters.AddWithValue("@id", versionId);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static TokenVersion ReadTokenVersion(SqliteDataReader reader)
    {
        return new TokenVersion
        {
            Id = reader.GetInt32(0),
            IdentityId = reader.GetInt32(1),
            VersionNumber = reader.GetInt32(2),
            IdToken = reader.GetString(3),
            AccessToken = reader.GetString(4),
            RefreshToken = reader.GetString(5),
            AccountId = reader.GetString(6),
            OpenAiApiKey = reader.IsDBNull(7) ? null : reader.GetString(7),
            LastRefresh = DateTime.Parse(reader.GetString(8)),
            CreatedAt = DateTime.Parse(reader.GetString(9)),
            IsCurrent = reader.GetInt32(10) == 1
        };
    }
}
