namespace CodexAuthManager.Core.Models;

/// <summary>
/// Represents a user identity with metadata extracted from JWT tokens
/// </summary>
public class Identity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property to token versions
    /// </summary>
    public List<TokenVersion> Versions { get; set; } = new();
}
