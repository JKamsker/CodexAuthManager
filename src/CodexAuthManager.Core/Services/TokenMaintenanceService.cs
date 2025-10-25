using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Services;

/// <summary>
/// Provides maintenance operations for token versions (deduplication, current flag repairs).
/// </summary>
public class TokenMaintenanceService
{
    private readonly IIdentityRepository _identityRepository;
    private readonly ITokenVersionRepository _tokenVersionRepository;

    public TokenMaintenanceService(
        IIdentityRepository identityRepository,
        ITokenVersionRepository tokenVersionRepository)
    {
        _identityRepository = identityRepository;
        _tokenVersionRepository = tokenVersionRepository;
    }

    /// <summary>
    /// Removes duplicate token combinations per identity and ensures the freshest token is current.
    /// </summary>
    public async Task<int> DeduplicateAndFixCurrentVersionsAsync()
    {
        var identities = (await _identityRepository.GetAllAsync()).ToList();
        int deleted = 0;

        foreach (var identity in identities)
        {
            var versions = (await _tokenVersionRepository.GetVersionsAsync(identity.Id)).ToList();
            if (!versions.Any())
            {
                continue;
            }

            var ordered = versions
                .OrderByDescending(v => NormalizeToUtc(v.LastRefresh))
                .ThenByDescending(v => NormalizeToUtc(v.CreatedAt))
                .ThenByDescending(v => v.VersionNumber)
                .ToList();

            var deduped = new List<TokenVersion>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var version in ordered)
            {
                var key = $"{version.IdToken}|{version.AccessToken}|{version.RefreshToken}";
                if (seen.Add(key))
                {
                    deduped.Add(version);
                }
                else
                {
                    await _tokenVersionRepository.DeleteAsync(version.Id);
                    deleted++;
                }
            }

            if (!deduped.Any())
            {
                continue;
            }

            var latest = deduped
                .OrderByDescending(v => NormalizeToUtc(v.LastRefresh))
                .ThenByDescending(v => NormalizeToUtc(v.CreatedAt))
                .ThenByDescending(v => v.VersionNumber)
                .First();

            if (!latest.IsCurrent || deduped.Any(v => v.Id != latest.Id && v.IsCurrent))
            {
                await _tokenVersionRepository.SetCurrentVersionAsync(identity.Id, latest.Id);
            }
        }

        return deleted;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        if (value == default)
        {
            return value;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
