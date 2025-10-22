namespace CodexAuthManager.Core.Models;

/// <summary>
/// Represents an immutable version of a token for an identity
/// </summary>
public class TokenVersion
{
    public int Id { get; set; }
    public int IdentityId { get; set; }
    public int VersionNumber { get; set; }

    public string IdToken { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string? OpenAiApiKey { get; set; }

    public DateTime LastRefresh { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Navigation property to parent identity
    /// </summary>
    public Identity? Identity { get; set; }
}
