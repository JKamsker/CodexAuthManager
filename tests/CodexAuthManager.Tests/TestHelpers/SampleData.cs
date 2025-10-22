using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// Provides sample test data
/// </summary>
public static class SampleData
{
    public static Identity CreateIdentity(int id = 1, string email = "test@example.com", bool isActive = false)
    {
        return new Identity
        {
            Id = id,
            Email = email,
            AccountId = $"account-{id}",
            UserId = $"user-{id}",
            PlanType = "plus",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static TokenVersion CreateTokenVersion(int id = 1, int identityId = 1, int versionNumber = 1, bool isCurrent = true)
    {
        return new TokenVersion
        {
            Id = id,
            IdentityId = identityId,
            VersionNumber = versionNumber,
            IdToken = $"id_token_{versionNumber}",
            AccessToken = $"access_token_{versionNumber}",
            RefreshToken = $"refresh_token_{versionNumber}",
            AccountId = $"account-{identityId}",
            OpenAiApiKey = null,
            LastRefresh = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsCurrent = isCurrent
        };
    }

    public static AuthToken CreateAuthToken()
    {
        return new AuthToken
        {
            OpenAiApiKey = null,
            Tokens = new TokenData
            {
                IdToken = CreateSampleIdToken(),
                AccessToken = "sample_access_token",
                RefreshToken = "sample_refresh_token",
                AccountId = "sample-account-id"
            },
            LastRefresh = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a sample JWT token with basic claims (not cryptographically valid, just for structure)
    /// </summary>
    public static string CreateSampleIdToken(
        string email = "test@example.com",
        string accountId = "test-account-id",
        string userId = "user-test123",
        string planType = "plus")
    {
        // This is a simplified JWT structure for testing
        // Header
        var header = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";

        // Payload with claims (including OpenAI-specific claims)
        var claims = new Dictionary<string, object>
        {
            ["email"] = email,
            ["email_verified"] = true,
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
        };

        // Add OpenAI auth claims if provided
        if (!string.IsNullOrEmpty(accountId) || !string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(planType))
        {
            var authClaims = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(accountId))
                authClaims["chatgpt_account_id"] = accountId;

            if (!string.IsNullOrEmpty(userId))
                authClaims["chatgpt_user_id"] = userId;

            if (!string.IsNullOrEmpty(planType))
                authClaims["chatgpt_plan_type"] = planType;

            claims["https://api.openai.com/auth"] = authClaims;
        }

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(claims);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payloadJson);
        var payload = Convert.ToBase64String(payloadBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Signature (fake)
        var signature = "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

        return $"{header}.{payload}.{signature}";
    }
}
