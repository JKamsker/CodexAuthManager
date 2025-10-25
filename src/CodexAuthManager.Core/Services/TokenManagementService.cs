using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Models;
using System;

namespace CodexAuthManager.Core.Services;

/// <summary>
/// Service for managing tokens with versioning and immutability
/// </summary>
public class TokenManagementService
{
    private readonly IIdentityRepository _identityRepository;
    private readonly ITokenVersionRepository _tokenVersionRepository;
    private readonly JwtDecoderService _jwtDecoder;

    public TokenManagementService(
        IIdentityRepository identityRepository,
        ITokenVersionRepository tokenVersionRepository,
        JwtDecoderService jwtDecoder)
    {
        _identityRepository = identityRepository;
        _tokenVersionRepository = tokenVersionRepository;
        _jwtDecoder = jwtDecoder;
    }

    /// <summary>
    /// Imports or updates a token, creating a new version if it differs from the current one.
    /// Matches identities by email or AccountId, and updates missing fields.
    /// </summary>
    public async Task<(int identityId, int versionId, bool isNew)> ImportOrUpdateTokenAsync(AuthToken authToken)
    {
        // Decode JWT to get metadata
        var metadata = _jwtDecoder.DecodeIdToken(authToken.Tokens.IdToken);

        // Try to find existing identity by multiple criteria
        Identity? identity = null;

        // Try matching by email first if available
        if (!string.IsNullOrWhiteSpace(metadata.Email))
        {
            identity = await _identityRepository.GetByEmailAsync(metadata.Email);
        }

        // Try matching by AccountId if not found by email
        if (identity == null && !string.IsNullOrWhiteSpace(metadata.AccountId))
        {
            identity = await _identityRepository.GetByAccountIdAsync(metadata.AccountId);
        }

        bool isNewIdentity = identity == null;
        bool identityUpdated = false;

        if (identity == null)
        {
            // Create new identity
            identity = new Identity
            {
                Email = metadata.Email ?? string.Empty,
                AccountId = metadata.AccountId ?? string.Empty,
                UserId = metadata.UserId ?? string.Empty,
                PlanType = metadata.PlanType ?? string.Empty,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            identity.Id = await _identityRepository.CreateAsync(identity);
        }
        else
        {
            // Update existing identity with missing or changed fields
            if (string.IsNullOrWhiteSpace(identity.Email) && !string.IsNullOrWhiteSpace(metadata.Email))
            {
                identity.Email = metadata.Email;
                identityUpdated = true;
            }

            if (string.IsNullOrWhiteSpace(identity.AccountId) && !string.IsNullOrWhiteSpace(metadata.AccountId))
            {
                identity.AccountId = metadata.AccountId;
                identityUpdated = true;
            }

            if (string.IsNullOrWhiteSpace(identity.UserId) && !string.IsNullOrWhiteSpace(metadata.UserId))
            {
                identity.UserId = metadata.UserId;
                identityUpdated = true;
            }

            if (string.IsNullOrWhiteSpace(identity.PlanType) && !string.IsNullOrWhiteSpace(metadata.PlanType))
            {
                identity.PlanType = metadata.PlanType;
                identityUpdated = true;
            }

            // Always update PlanType if it changed (plan can be upgraded/downgraded)
            if (!string.IsNullOrWhiteSpace(metadata.PlanType) && identity.PlanType != metadata.PlanType)
            {
                identity.PlanType = metadata.PlanType;
                identityUpdated = true;
            }
        }

        var currentVersion = await _tokenVersionRepository.GetCurrentVersionAsync(identity.Id);
        bool tokensMatchCurrent = currentVersion != null &&
                                  currentVersion.IdToken == authToken.Tokens.IdToken &&
                                  currentVersion.AccessToken == authToken.Tokens.AccessToken &&
                                  currentVersion.RefreshToken == authToken.Tokens.RefreshToken;

        var incomingTimestamp = DetermineTokenTimestamp(authToken, metadata);
        var currentTimestamp = currentVersion != null
            ? NormalizeToUtc(currentVersion.LastRefresh)
            : DateTime.MinValue;

        TokenVersion? matchingExisting = null;
        if (!tokensMatchCurrent)
        {
            matchingExisting = await _tokenVersionRepository.FindByTokenAsync(
                identity.Id,
                authToken.Tokens.IdToken,
                authToken.Tokens.AccessToken,
                authToken.Tokens.RefreshToken);
        }

        bool isOlderThanCurrent = currentVersion != null && incomingTimestamp < currentTimestamp && !tokensMatchCurrent;

        if (!tokensMatchCurrent && isOlderThanCurrent)
        {
            if (identityUpdated)
            {
                identity.UpdatedAt = DateTime.UtcNow;
                await _identityRepository.UpdateAsync(identity);
            }

            return (identity.Id, currentVersion!.Id, false);
        }

        if (tokensMatchCurrent && !identityUpdated)
        {
            return (identity.Id, currentVersion!.Id, false);
        }

        if (!tokensMatchCurrent && matchingExisting != null)
        {
            if (!matchingExisting.IsCurrent)
            {
                await _tokenVersionRepository.SetCurrentVersionAsync(identity.Id, matchingExisting.Id);
            }

            identity.UpdatedAt = DateTime.UtcNow;
            await _identityRepository.UpdateAsync(identity);

            return (identity.Id, matchingExisting.Id, false);
        }

        if (!tokensMatchCurrent)
        {
            // Get next version number
            var versions = await _tokenVersionRepository.GetVersionsAsync(identity.Id);
            int nextVersionNumber = versions.Any() ? versions.Max(v => v.VersionNumber) + 1 : 1;

            // Create new version
            var newVersion = new TokenVersion
            {
                IdentityId = identity.Id,
                VersionNumber = nextVersionNumber,
                IdToken = authToken.Tokens.IdToken,
                AccessToken = authToken.Tokens.AccessToken,
                RefreshToken = authToken.Tokens.RefreshToken,
                AccountId = authToken.Tokens.AccountId,
                OpenAiApiKey = authToken.OpenAiApiKey,
                LastRefresh = incomingTimestamp,
                CreatedAt = DateTime.UtcNow,
                IsCurrent = false
            };

            int versionId = await _tokenVersionRepository.CreateAsync(newVersion);

            // Set as current version
            await _tokenVersionRepository.SetCurrentVersionAsync(identity.Id, versionId);

            // Update identity metadata (always update timestamp and conditionally update other fields)
            identity.UpdatedAt = DateTime.UtcNow;
            await _identityRepository.UpdateAsync(identity);

            return (identity.Id, versionId, isNewIdentity);
        }

        // Tokens already current but metadata changed
        if (identityUpdated)
        {
            identity.UpdatedAt = DateTime.UtcNow;
            await _identityRepository.UpdateAsync(identity);
        }

        return (identity.Id, currentVersion?.Id ?? matchingExisting?.Id ?? 0, isNewIdentity);
    }

    /// <summary>
    /// Rollback to a previous version by creating a new version based on the old one
    /// </summary>
    public async Task<int> RollbackToVersionAsync(int identityId, int versionId)
    {
        var versionToRestore = await _tokenVersionRepository.GetByIdAsync(versionId);
        if (versionToRestore == null || versionToRestore.IdentityId != identityId)
        {
            throw new ArgumentException("Version not found or doesn't belong to the specified identity");
        }

        // Get next version number
        var versions = await _tokenVersionRepository.GetVersionsAsync(identityId);
        int nextVersionNumber = versions.Max(v => v.VersionNumber) + 1;

        // Create new version based on the old one
        var restoredVersion = new TokenVersion
        {
            IdentityId = identityId,
            VersionNumber = nextVersionNumber,
            IdToken = versionToRestore.IdToken,
            AccessToken = versionToRestore.AccessToken,
            RefreshToken = versionToRestore.RefreshToken,
            AccountId = versionToRestore.AccountId,
            OpenAiApiKey = versionToRestore.OpenAiApiKey,
            LastRefresh = versionToRestore.LastRefresh,
            CreatedAt = DateTime.UtcNow,
            IsCurrent = false
        };

        int newVersionId = await _tokenVersionRepository.CreateAsync(restoredVersion);
        await _tokenVersionRepository.SetCurrentVersionAsync(identityId, newVersionId);

        return newVersionId;
    }

    /// <summary>
    /// Gets the current token for an identity
    /// </summary>
    public async Task<AuthToken?> GetCurrentTokenAsync(int identityId)
    {
        var version = await _tokenVersionRepository.GetCurrentVersionAsync(identityId);
        if (version == null) return null;

        return new AuthToken
        {
            OpenAiApiKey = version.OpenAiApiKey,
            Tokens = new TokenData
            {
                IdToken = version.IdToken,
                AccessToken = version.AccessToken,
                RefreshToken = version.RefreshToken,
                AccountId = version.AccountId
            },
            LastRefresh = version.LastRefresh
        };
    }

    private static DateTime DetermineTokenTimestamp(AuthToken authToken, JwtMetadata metadata)
    {
        if (authToken.LastRefresh != default)
        {
            return NormalizeToUtc(authToken.LastRefresh);
        }

        if (metadata.IssuedAt != default)
        {
            return NormalizeToUtc(metadata.IssuedAt);
        }

        return DateTime.UtcNow;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        if (value == default)
        {
            return value;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
