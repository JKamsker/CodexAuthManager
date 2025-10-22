using CodexAuthManager.Core.Models;
using Microsoft.Data.Sqlite;

namespace CodexAuthManager.Core.Data;

/// <summary>
/// SQLite implementation of identity repository
/// </summary>
public class IdentityRepository : IIdentityRepository
{
    private readonly TokenDatabase _database;

    public IdentityRepository(TokenDatabase database)
    {
        _database = database;
    }

    public async Task<Identity?> GetByIdAsync(int id)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Email, AccountId, UserId, PlanType, IsActive, CreatedAt, UpdatedAt
            FROM Identities
            WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return reader.Read() ? ReadIdentity(reader) : null;
    }

    public async Task<Identity?> GetByEmailAsync(string email)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Email, AccountId, UserId, PlanType, IsActive, CreatedAt, UpdatedAt
            FROM Identities
            WHERE Email = @email";
        command.Parameters.AddWithValue("@email", email);

        await using var reader = await command.ExecuteReaderAsync();
        return reader.Read() ? ReadIdentity(reader) : null;
    }

    public async Task<Identity?> GetByAccountIdAsync(string accountId)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = """
                              SELECT Id, Email, AccountId, UserId, PlanType, IsActive, CreatedAt, UpdatedAt
                              FROM Identities
                              WHERE AccountId = @accountId
                              """;
        command.Parameters.AddWithValue("@accountId", accountId);

        await using var reader = await command.ExecuteReaderAsync();
        return reader.Read() ? ReadIdentity(reader) : null;
    }

    public async Task<Identity?> GetActiveIdentityAsync()
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Email, AccountId, UserId, PlanType, IsActive, CreatedAt, UpdatedAt
            FROM Identities
            WHERE IsActive = 1
            LIMIT 1";

        await using var reader = await command.ExecuteReaderAsync();
        return reader.Read() ? ReadIdentity(reader) : null;
    }

    public async Task<IEnumerable<Identity>> GetAllAsync()
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Email, AccountId, UserId, PlanType, IsActive, CreatedAt, UpdatedAt
            FROM Identities
            ORDER BY Email";

        var identities = new List<Identity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            identities.Add(ReadIdentity(reader));
        }
        return identities;
    }

    public async Task<int> CreateAsync(Identity identity)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Identities (Email, AccountId, UserId, PlanType, IsActive, CreatedAt, UpdatedAt)
            VALUES (@email, @accountId, @userId, @planType, @isActive, @createdAt, @updatedAt);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@email", identity.Email);
        command.Parameters.AddWithValue("@accountId", identity.AccountId);
        command.Parameters.AddWithValue("@userId", identity.UserId);
        command.Parameters.AddWithValue("@planType", identity.PlanType);
        command.Parameters.AddWithValue("@isActive", identity.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@createdAt", identity.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@updatedAt", identity.UpdatedAt.ToString("O"));

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task UpdateAsync(Identity identity)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            UPDATE Identities
            SET Email = @email, AccountId = @accountId, UserId = @userId,
                PlanType = @planType, IsActive = @isActive, UpdatedAt = @updatedAt
            WHERE Id = @id";

        command.Parameters.AddWithValue("@id", identity.Id);
        command.Parameters.AddWithValue("@email", identity.Email);
        command.Parameters.AddWithValue("@accountId", identity.AccountId);
        command.Parameters.AddWithValue("@userId", identity.UserId);
        command.Parameters.AddWithValue("@planType", identity.PlanType);
        command.Parameters.AddWithValue("@isActive", identity.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@updatedAt", identity.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM Identities WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetActiveAsync(int id)
    {
        await using var transaction = _database.Connection.BeginTransaction();
        try
        {
            // Deactivate all identities
            await using (var command = _database.Connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE Identities SET IsActive = 0";
                await command.ExecuteNonQueryAsync();
            }

            // Activate the specified identity
            await using (var command = _database.Connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE Identities SET IsActive = 1 WHERE Id = @id";
                command.Parameters.AddWithValue("@id", id);
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

    private static Identity ReadIdentity(SqliteDataReader reader)
    {
        return new Identity
        {
            Id = reader.GetInt32(0),
            Email = reader.GetString(1),
            AccountId = reader.GetString(2),
            UserId = reader.GetString(3),
            PlanType = reader.GetString(4),
            IsActive = reader.GetInt32(5) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(6)),
            UpdatedAt = DateTime.Parse(reader.GetString(7))
        };
    }
}
