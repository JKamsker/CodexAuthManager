using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Data;

/// <summary>
/// Repository interface for token version operations
/// </summary>
public interface ITokenVersionRepository
{
    Task<TokenVersion?> GetByIdAsync(int id);
    Task<TokenVersion?> GetCurrentVersionAsync(int identityId);
    Task<IEnumerable<TokenVersion>> GetVersionsAsync(int identityId);
    Task<int> CreateAsync(TokenVersion tokenVersion);
    Task SetCurrentVersionAsync(int identityId, int versionId);
    Task<TokenVersion?> FindByTokenAsync(int identityId, string idToken, string accessToken, string refreshToken);
    Task DeleteAsync(int id);
}
