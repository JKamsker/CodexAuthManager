using CodexAuthManager.Core.Models;
using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Services;

public class TokenManagementServiceTests : IDisposable
{
    private readonly TestFixture _fixture;

    public TokenManagementServiceTests()
    {
        _fixture = new TestFixture();
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldCreateNewIdentity_WhenNotExists()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();

        // Act
        var (identityId, versionId, isNew) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        Assert.True(identityId > 0);
        Assert.True(versionId > 0);
        Assert.True(isNew);

        var identity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(identity);
        Assert.Equal("test@example.com", identity.Email);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldCreateFirstVersion_ForNewIdentity()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();

        // Act
        var (identityId, versionId, isNew) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        var version = await _fixture.TokenVersionRepository.GetByIdAsync(versionId);
        Assert.NotNull(version);
        Assert.Equal(1, version.VersionNumber);
        Assert.True(version.IsCurrent);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldNotCreateNewVersion_WhenTokensUnchanged()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        var (identityId, versionId1, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Act - Import same token again
        var (identityId2, versionId2, isNew) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        Assert.Equal(identityId, identityId2);
        Assert.Equal(versionId1, versionId2);
        Assert.False(isNew);

        var versions = await _fixture.TokenVersionRepository.GetVersionsAsync(identityId);
        Assert.Single(versions); // Still only 1 version
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldCreateNewVersion_WhenTokensChanged()
    {
        // Arrange
        var authToken1 = SampleData.CreateAuthToken();
        var (identityId, versionId1, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.AccessToken = "new_access_token";

        // Act
        var (identityId2, versionId2, isNew) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken2);

        // Assert
        Assert.Equal(identityId, identityId2);
        Assert.NotEqual(versionId1, versionId2);
        Assert.False(isNew); // Identity is not new, just the version

        var versions = (await _fixture.TokenVersionRepository.GetVersionsAsync(identityId)).ToList();
        Assert.Equal(2, versions.Count);
        Assert.Equal(2, versions[0].VersionNumber); // Latest version
        Assert.True(versions[0].IsCurrent);
        Assert.False(versions[1].IsCurrent);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldSetNewVersionAsCurrent()
    {
        // Arrange
        var authToken1 = SampleData.CreateAuthToken();
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.RefreshToken = "new_refresh_token";

        // Act
        var (identityId, versionId, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken2);

        // Assert
        var currentVersion = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);
        Assert.NotNull(currentVersion);
        Assert.Equal(versionId, currentVersion.Id);
        Assert.Equal(2, currentVersion.VersionNumber);
    }

    [Fact]
    public async Task RollbackToVersionAsync_ShouldCreateNewVersionBasedOnOldOne()
    {
        // Arrange
        var authToken1 = SampleData.CreateAuthToken();
        authToken1.Tokens.AccessToken = "original_token";
        var (identityId, versionId1, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.AccessToken = "updated_token";
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken2);

        // Act - Rollback to version 1
        var newVersionId = await _fixture.TokenManagement.RollbackToVersionAsync(identityId, versionId1);

        // Assert
        var newVersion = await _fixture.TokenVersionRepository.GetByIdAsync(newVersionId);
        Assert.NotNull(newVersion);
        Assert.Equal(3, newVersion.VersionNumber); // New version number
        Assert.Equal("original_token", newVersion.AccessToken); // Original token data
        Assert.True(newVersion.IsCurrent);
    }

    [Fact]
    public async Task RollbackToVersionAsync_ShouldThrow_WhenVersionNotFound()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _fixture.TokenManagement.RollbackToVersionAsync(identityId, 999));
    }

    [Fact]
    public async Task RollbackToVersionAsync_ShouldThrow_WhenVersionBelongsToDifferentIdentity()
    {
        // Arrange
        var authToken1 = SampleData.CreateAuthToken();
        var (identityId1, versionId1, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.IdToken = SampleData.CreateSampleIdToken("other@example.com", accountId: "other-account-id");
        var (identityId2, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken2);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _fixture.TokenManagement.RollbackToVersionAsync(identityId2, versionId1));
    }

    [Fact]
    public async Task GetCurrentTokenAsync_ShouldReturnAuthToken_WhenVersionExists()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Act
        var result = await _fixture.TokenManagement.GetCurrentTokenAsync(identityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(authToken.Tokens.AccessToken, result.Tokens.AccessToken);
        Assert.Equal(authToken.Tokens.RefreshToken, result.Tokens.RefreshToken);
    }

    [Fact]
    public async Task GetCurrentTokenAsync_ShouldReturnNull_WhenNoCurrentVersion()
    {
        // Arrange - Create identity but no version
        var identity = SampleData.CreateIdentity(0, "test@example.com");
        var identityId = await _fixture.IdentityRepository.CreateAsync(identity);

        // Act
        var result = await _fixture.TokenManagement.GetCurrentTokenAsync(identityId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldIncrementVersionNumbers_Sequentially()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();

        // Act - Import 5 times with different tokens
        for (int i = 1; i <= 5; i++)
        {
            authToken.Tokens.AccessToken = $"token_{i}";
            await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);
        }

        // Assert
        var identity = await _fixture.IdentityRepository.GetByEmailAsync("test@example.com");
        Assert.NotNull(identity);

        var versions = (await _fixture.TokenVersionRepository.GetVersionsAsync(identity.Id)).ToList();
        Assert.Equal(5, versions.Count);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(5 - i, versions[i].VersionNumber); // Descending order
        }
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldFillAllColumns_ForNewIdentity()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();

        // Act
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        var identity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(identity);
        Assert.Equal("test@example.com", identity.Email);
        Assert.Equal("test-account-id", identity.AccountId);
        Assert.Equal("user-test123", identity.UserId);
        Assert.Equal("plus", identity.PlanType);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldUpdateMissingColumns_InExistingIdentity()
    {
        // Arrange - Create identity with only email
        var identity = new Identity
        {
            Email = "test@example.com",
            AccountId = string.Empty,
            UserId = string.Empty,
            PlanType = string.Empty,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        identity.Id = await _fixture.IdentityRepository.CreateAsync(identity);

        var authToken = SampleData.CreateAuthToken();

        // Act - Import token with full metadata
        var (identityId, _, isNew) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        Assert.Equal(identity.Id, identityId);
        Assert.False(isNew); // Not a new identity

        var updatedIdentity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(updatedIdentity);
        Assert.Equal("test@example.com", updatedIdentity.Email);
        Assert.Equal("test-account-id", updatedIdentity.AccountId);
        Assert.Equal("user-test123", updatedIdentity.UserId);
        Assert.Equal("plus", updatedIdentity.PlanType);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldMatchByAccountId_WhenEmailNotFound()
    {
        // Arrange - Create identity with AccountId but different email
        var identity = new Identity
        {
            Email = "old@example.com",
            AccountId = "test-account-id",
            UserId = "user-test123",
            PlanType = "plus",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        identity.Id = await _fixture.IdentityRepository.CreateAsync(identity);

        var authToken = SampleData.CreateAuthToken(); // Has email "test@example.com"

        // Act - Import should match by AccountId, not create new identity
        var (identityId, _, isNew) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        Assert.Equal(identity.Id, identityId);
        Assert.False(isNew); // Not a new identity

        var allIdentities = await _fixture.IdentityRepository.GetAllAsync();
        Assert.Single(allIdentities); // Should still only have 1 identity
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldUpdateEmail_WhenMissing()
    {
        // Arrange - Create identity with only AccountId
        var identity = new Identity
        {
            Email = string.Empty,
            AccountId = "test-account-id",
            UserId = "user-test123",
            PlanType = "plus",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        identity.Id = await _fixture.IdentityRepository.CreateAsync(identity);

        var authToken = SampleData.CreateAuthToken();

        // Act
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        var updatedIdentity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(updatedIdentity);
        Assert.Equal("test@example.com", updatedIdentity.Email);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldUpdatePlanType_WhenChanged()
    {
        // Arrange - Import with initial plan type
        var authToken = SampleData.CreateAuthToken();
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Change plan type in token
        var idTokenWithPro = SampleData.CreateSampleIdToken("test@example.com", planType: "pro");
        authToken.Tokens.IdToken = idTokenWithPro;
        authToken.Tokens.AccessToken = "new_token"; // Change token to force version creation

        // Act
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Assert
        var updatedIdentity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(updatedIdentity);
        Assert.Equal("pro", updatedIdentity.PlanType);
    }

    [Fact]
    public async Task ImportOrUpdateTokenAsync_ShouldNotOverwriteExistingFields_WithEmptyValues()
    {
        // Arrange - Create identity with all fields populated
        var authToken = SampleData.CreateAuthToken();
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Create token with empty metadata (simulating incomplete JWT)
        var incompleteIdToken = SampleData.CreateSampleIdToken("test@example.com", accountId: "", userId: "", planType: "");
        var incompleteToken = SampleData.CreateAuthToken();
        incompleteToken.Tokens.IdToken = incompleteIdToken;
        incompleteToken.Tokens.AccessToken = "different_token";

        // Act
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(incompleteToken);

        // Assert - Original values should be preserved
        var identity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(identity);
        Assert.Equal("test-account-id", identity.AccountId);
        Assert.Equal("user-test123", identity.UserId);
        Assert.Equal("plus", identity.PlanType); // Should keep original value
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
