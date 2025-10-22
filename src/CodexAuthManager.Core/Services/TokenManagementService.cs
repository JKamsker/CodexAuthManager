using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Models;

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
    /// Imports or updates a token, creating a new version if it differs from the current one
    /// </summary>
    public async Task<(int identityId, int versionId, bool isNew)> ImportOrUpdateTokenAsync(AuthToken authToken)
    {
        // Decode JWT to get metadata
        var metadata = _jwtDecoder.DecodeIdToken(authToken.Tokens.IdToken);

        // Check if identity exists
        var identity = await _identityRepository.GetByEmailAsync(metadata.Email);
        bool isNewIdentity = identity == null;

        if (identity == null)
        {
            // Create new identity
            identity = new Identity
            {
                Email = metadata.Email,
                AccountId = metadata.AccountId,
                UserId = metadata.UserId,
                PlanType = metadata.PlanType,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            identity.Id = await _identityRepository.CreateAsync(identity);
        }

        // Check if current version differs
        var currentVersion = await _tokenVersionRepository.GetCurrentVersionAsync(identity.Id);
        bool tokensChanged = currentVersion == null ||
                            currentVersion.IdToken != authToken.Tokens.IdToken ||
                            currentVersion.AccessToken != authToken.Tokens.AccessToken ||
                            currentVersion.RefreshToken != authToken.Tokens.RefreshToken;

        if (!tokensChanged && currentVersion != null)
        {
            return (identity.Id, currentVersion.Id, false);
        }

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
            LastRefresh = authToken.LastRefresh,
            CreatedAt = DateTime.UtcNow,
            IsCurrent = false
        };

        int versionId = await _tokenVersionRepository.CreateAsync(newVersion);

        // Set as current version
        await _tokenVersionRepository.SetCurrentVersionAsync(identity.Id, versionId);

        // Update identity metadata
        identity.UpdatedAt = DateTime.UtcNow;
        identity.PlanType = metadata.PlanType;
        await _identityRepository.UpdateAsync(identity);

        return (identity.Id, versionId, isNewIdentity);
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
}
