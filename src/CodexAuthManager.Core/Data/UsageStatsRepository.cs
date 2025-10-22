using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Data;

public class UsageStatsRepository : IUsageStatsRepository
{
    private readonly TokenDatabase _database;

    public UsageStatsRepository(TokenDatabase database)
    {
        _database = database;
    }

    public async Task<int> CreateAsync(UsageStats stats)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO UsageStats (
                IdentityId,
                FiveHourLimitPercent,
                FiveHourLimitResetTime,
                WeeklyLimitPercent,
                WeeklyLimitResetTime,
                CapturedAt
            ) VALUES (
                @IdentityId,
                @FiveHourLimitPercent,
                @FiveHourLimitResetTime,
                @WeeklyLimitPercent,
                @WeeklyLimitResetTime,
                @CapturedAt
            );
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@IdentityId", stats.IdentityId);
        command.Parameters.AddWithValue("@FiveHourLimitPercent", stats.FiveHourLimitPercent);
        command.Parameters.AddWithValue("@FiveHourLimitResetTime", stats.FiveHourLimitResetTime.ToString("O"));
        command.Parameters.AddWithValue("@WeeklyLimitPercent", stats.WeeklyLimitPercent);
        command.Parameters.AddWithValue("@WeeklyLimitResetTime", stats.WeeklyLimitResetTime.ToString("O"));
        command.Parameters.AddWithValue("@CapturedAt", stats.CapturedAt.ToString("O"));

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<UsageStats?> GetLatestAsync(int identityId)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT
                Id,
                IdentityId,
                FiveHourLimitPercent,
                FiveHourLimitResetTime,
                WeeklyLimitPercent,
                WeeklyLimitResetTime,
                CapturedAt
            FROM UsageStats
            WHERE IdentityId = @IdentityId
            ORDER BY CapturedAt DESC
            LIMIT 1
        ";

        command.Parameters.AddWithValue("@IdentityId", identityId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new UsageStats
            {
                Id = reader.GetInt32(0),
                IdentityId = reader.GetInt32(1),
                FiveHourLimitPercent = reader.GetInt32(2),
                FiveHourLimitResetTime = DateTime.Parse(reader.GetString(3)),
                WeeklyLimitPercent = reader.GetInt32(4),
                WeeklyLimitResetTime = DateTime.Parse(reader.GetString(5)),
                CapturedAt = DateTime.Parse(reader.GetString(6))
            };
        }

        return null;
    }

    public async Task<IEnumerable<UsageStats>> GetHistoryAsync(int identityId, int limit = 10)
    {
        await using var command = _database.Connection.CreateCommand();
        command.CommandText = @"
            SELECT
                Id,
                IdentityId,
                FiveHourLimitPercent,
                FiveHourLimitResetTime,
                WeeklyLimitPercent,
                WeeklyLimitResetTime,
                CapturedAt
            FROM UsageStats
            WHERE IdentityId = @IdentityId
            ORDER BY CapturedAt DESC
            LIMIT @Limit
        ";

        command.Parameters.AddWithValue("@IdentityId", identityId);
        command.Parameters.AddWithValue("@Limit", limit);

        var stats = new List<UsageStats>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stats.Add(new UsageStats
            {
                Id = reader.GetInt32(0),
                IdentityId = reader.GetInt32(1),
                FiveHourLimitPercent = reader.GetInt32(2),
                FiveHourLimitResetTime = DateTime.Parse(reader.GetString(3)),
                WeeklyLimitPercent = reader.GetInt32(4),
                WeeklyLimitResetTime = DateTime.Parse(reader.GetString(5)),
                CapturedAt = DateTime.Parse(reader.GetString(6))
            });
        }

        return stats;
    }
}
