namespace CodexAuthManager.Core.Models;

/// <summary>
/// Represents the token data structure from auth.json
/// </summary>
public class TokenData
{
    public string IdToken { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
}
