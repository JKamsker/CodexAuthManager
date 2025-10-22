namespace CodexAuthManager.Core.Models;

/// <summary>
/// Represents decoded JWT token metadata
/// </summary>
public class JwtMetadata
{
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public DateTime? SubscriptionActiveStart { get; set; }
    public DateTime? SubscriptionActiveUntil { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
