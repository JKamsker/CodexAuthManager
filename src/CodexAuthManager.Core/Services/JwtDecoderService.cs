using System.IdentityModel.Tokens.Jwt;
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

        // Extract OpenAI-specific claims
        var authClaims = token.Claims.Where(c => c.Type == "https://api.openai.com/auth").ToList();
        if (authClaims.Any())
        {
            // The auth claim contains a JSON object, we need to parse the value
            var authClaim = authClaims.First();
            // For simplicity, we'll extract from individual nested claims if the library provides them
            // Otherwise we'd need to parse the JSON string
        }

        // Try to extract from individual claims
        foreach (var claim in token.Claims)
        {
            if (claim.Type.Contains("chatgpt_account_id"))
                metadata.AccountId = claim.Value;
            else if (claim.Type.Contains("chatgpt_user_id") || claim.Type.Contains("user_id"))
                metadata.UserId = claim.Value;
            else if (claim.Type.Contains("chatgpt_plan_type"))
                metadata.PlanType = claim.Value;
            else if (claim.Type.Contains("chatgpt_subscription_active_start"))
            {
                if (DateTime.TryParse(claim.Value, out var startDate))
                    metadata.SubscriptionActiveStart = startDate;
            }
            else if (claim.Type.Contains("chatgpt_subscription_active_until"))
            {
                if (DateTime.TryParse(claim.Value, out var endDate))
                    metadata.SubscriptionActiveUntil = endDate;
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
