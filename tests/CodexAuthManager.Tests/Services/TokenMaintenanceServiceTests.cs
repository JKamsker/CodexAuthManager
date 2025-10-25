using CodexAuthManager.Core.Services;
using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Services;

public class TokenMaintenanceServiceTests : IDisposable
{
    private readonly TestFixture _fixture;
    private readonly TokenMaintenanceService _maintenanceService;

    public TokenMaintenanceServiceTests()
    {
        _fixture = new TestFixture();
        _maintenanceService = new TokenMaintenanceService(_fixture.IdentityRepository, _fixture.TokenVersionRepository);
    }

    [Fact]
    public async Task Deduplicate_ShouldRemoveOlderDuplicateTokens()
    {
        var identity = SampleData.CreateIdentity(0, "dup@example.com");
        var identityId = await _fixture.IdentityRepository.CreateAsync(identity);

        var duplicateA = SampleData.CreateTokenVersion(0, identityId, 1, isCurrent: true);
        duplicateA.LastRefresh = DateTime.UtcNow.AddHours(-6);
        duplicateA.IdToken = "dup-id";
        duplicateA.AccessToken = "dup-access";
        duplicateA.RefreshToken = "dup-refresh";
        var keepId = await _fixture.TokenVersionRepository.CreateAsync(duplicateA);

        var duplicateB = SampleData.CreateTokenVersion(0, identityId, 2, isCurrent: false);
        duplicateB.LastRefresh = DateTime.UtcNow.AddHours(-12);
        duplicateB.IdToken = duplicateA.IdToken;
        duplicateB.AccessToken = duplicateA.AccessToken;
        duplicateB.RefreshToken = duplicateA.RefreshToken;
        await _fixture.TokenVersionRepository.CreateAsync(duplicateB);

        var removed = await _maintenanceService.DeduplicateAndFixCurrentVersionsAsync();

        Assert.Equal(1, removed);
        var versions = (await _fixture.TokenVersionRepository.GetVersionsAsync(identityId)).ToList();
        Assert.Single(versions);
        Assert.Equal(keepId, versions[0].Id);
        Assert.True(versions[0].IsCurrent);
    }

    [Fact]
    public async Task Deduplicate_ShouldSetNewestTokenAsCurrent()
    {
        var identity = SampleData.CreateIdentity(0, "current@example.com");
        var identityId = await _fixture.IdentityRepository.CreateAsync(identity);

        var older = SampleData.CreateTokenVersion(0, identityId, 1, isCurrent: true);
        older.LastRefresh = DateTime.UtcNow.AddHours(-24);
        older.AccessToken = "older";
        var olderId = await _fixture.TokenVersionRepository.CreateAsync(older);

        var newer = SampleData.CreateTokenVersion(0, identityId, 2, isCurrent: false);
        newer.LastRefresh = DateTime.UtcNow;
        newer.AccessToken = "newer";
        var newerId = await _fixture.TokenVersionRepository.CreateAsync(newer);

        var removed = await _maintenanceService.DeduplicateAndFixCurrentVersionsAsync();

        Assert.Equal(0, removed);
        var current = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);
        Assert.NotNull(current);
        Assert.Equal(newerId, current!.Id);
        Assert.Equal("newer", current.AccessToken);

        var olderVersion = await _fixture.TokenVersionRepository.GetByIdAsync(olderId);
        Assert.False(olderVersion!.IsCurrent);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}

