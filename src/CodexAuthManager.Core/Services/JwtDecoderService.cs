using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Services;

/// <summary>
/// Service for decoding JWT tokens and extracting metadata
/// </summary>
public class JwtDecoderService
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtMetadata DecodeIdToken(string idToken)
    {
        var token = _tokenHandler.ReadJwtToken(idToken);
        var metadata = new JwtMetadata
        {
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(
                long.Parse(token.Claims.First(c => c.Type == "iat").Value)).DateTime,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(
                long.Parse(token.Claims.First(c => c.Type == "exp").Value)).DateTime
        };

        // Extract email
        var emailClaim = token.Claims.FirstOrDefault(c => c.Type == "email");
        if (emailClaim != null)
            metadata.Email = emailClaim.Value;

        var emailVerifiedClaim = token.Claims.FirstOrDefault(c => c.Type == "email_verified");
        if (emailVerifiedClaim != null)
            metadata.EmailVerified = bool.Parse(emailVerifiedClaim.Value);

        // Extract OpenAI-specific claims from nested JSON
        var authClaim = token.Claims.FirstOrDefault(c => c.Type == "https://api.openai.com/auth");
        if (authClaim != null)
        {
            try
            {
                // Parse the JSON string in the auth claim
                using var authDoc = JsonDocument.Parse(authClaim.Value);
                var authRoot = authDoc.RootElement;

                if (authRoot.TryGetProperty("chatgpt_account_id", out var accountIdElem))
                    metadata.AccountId = accountIdElem.GetString();

                if (authRoot.TryGetProperty("chatgpt_user_id", out var userIdElem))
                    metadata.UserId = userIdElem.GetString();

                if (authRoot.TryGetProperty("chatgpt_plan_type", out var planTypeElem))
                    metadata.PlanType = planTypeElem.GetString();

                if (authRoot.TryGetProperty("chatgpt_subscription_active_start", out var startElem))
                {
                    if (DateTime.TryParse(startElem.GetString(), out var startDate))
                        metadata.SubscriptionActiveStart = startDate;
                }

                if (authRoot.TryGetProperty("chatgpt_subscription_active_until", out var endElem))
                {
                    if (DateTime.TryParse(endElem.GetString(), out var endDate))
                        metadata.SubscriptionActiveUntil = endDate;
                }
            }
            catch
            {
                // If parsing fails, try fallback approach
            }
        }

        // Try to extract from individual claims (fallback for flattened claims)
        if (string.IsNullOrEmpty(metadata.AccountId) || string.IsNullOrEmpty(metadata.UserId) || string.IsNullOrEmpty(metadata.PlanType))
        {
            foreach (var claim in token.Claims)
            {
                if (string.IsNullOrEmpty(metadata.AccountId) && claim.Type.Contains("chatgpt_account_id"))
                    metadata.AccountId = claim.Value;
                else if (string.IsNullOrEmpty(metadata.UserId) && (claim.Type.Contains("chatgpt_user_id") || claim.Type.Contains("user_id")))
                    metadata.UserId = claim.Value;
                else if (string.IsNullOrEmpty(metadata.PlanType) && claim.Type.Contains("chatgpt_plan_type"))
                    metadata.PlanType = claim.Value;
                else if (metadata.SubscriptionActiveStart == null && claim.Type.Contains("chatgpt_subscription_active_start"))
                {
                    if (DateTime.TryParse(claim.Value, out var startDate))
                        metadata.SubscriptionActiveStart = startDate;
                }
                else if (metadata.SubscriptionActiveUntil == null && claim.Type.Contains("chatgpt_subscription_active_until"))
                {
                    if (DateTime.TryParse(claim.Value, out var endDate))
                        metadata.SubscriptionActiveUntil = endDate;
                }
            }
        }

        return metadata;
    }

    public bool TryDecodeIdToken(string idToken, out JwtMetadata? metadata)
    {
        try
        {
            metadata = DecodeIdToken(idToken);
            return true;
        }
        catch
        {
            metadata = null;
            return false;
        }
    }
}
