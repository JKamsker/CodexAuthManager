using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Data;

/// <summary>
/// Repository interface for identity operations
/// </summary>
public interface IIdentityRepository
{
    Task<Identity?> GetByIdAsync(int id);
    Task<Identity?> GetByEmailAsync(string email);
    Task<Identity?> GetActiveIdentityAsync();
    Task<IEnumerable<Identity>> GetAllAsync();
    Task<int> CreateAsync(Identity identity);
    Task UpdateAsync(Identity identity);
    Task DeleteAsync(int id);
    Task SetActiveAsync(int id);
}
