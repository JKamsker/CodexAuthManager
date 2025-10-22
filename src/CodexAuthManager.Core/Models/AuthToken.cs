namespace CodexAuthManager.Core.Models;

/// <summary>
/// Represents the complete auth.json file structure
/// </summary>
public class AuthToken
{
    public string? OpenAiApiKey { get; set; }
    public TokenData Tokens { get; set; } = new();
    public DateTime LastRefresh { get; set; }
}
