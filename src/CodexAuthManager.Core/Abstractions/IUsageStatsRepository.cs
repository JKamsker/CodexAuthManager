using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Abstractions;

public interface IUsageStatsRepository
{
    Task<int> CreateAsync(UsageStats stats);
    Task<UsageStats?> GetLatestAsync(int identityId);
    Task<IEnumerable<UsageStats>> GetHistoryAsync(int identityId, int limit = 10);
}
