using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Data;

public class TokenVersionRepositoryTests : IDisposable
{
    private readonly TestFixture _fixture;

    public TokenVersionRepositoryTests()
    {
        _fixture = new TestFixture();
    }

    private async Task<int> CreateTestIdentityAsync(string email = "test@example.com")
    {
        var identity = SampleData.CreateIdentity(0, email);
        return await _fixture.IdentityRepository.CreateAsync(identity);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateTokenVersion_AndReturnId()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        var version = SampleData.CreateTokenVersion(0, identityId, 1);

        // Act
        var id = await _fixture.TokenVersionRepository.CreateAsync(version);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTokenVersion_WhenExists()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        var version = SampleData.CreateTokenVersion(0, identityId, 1);
        var id = await _fixture.TokenVersionRepository.CreateAsync(version);

        // Act
        var result = await _fixture.TokenVersionRepository.GetByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
        Assert.Equal(identityId, result.IdentityId);
        Assert.Equal(1, result.VersionNumber);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _fixture.TokenVersionRepository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentVersionAsync_ShouldReturnCurrentVersion()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 1, false));
        var currentId = await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 2, true));

        // Act
        var result = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(currentId, result.Id);
        Assert.Equal(2, result.VersionNumber);
        Assert.True(result.IsCurrent);
    }

    [Fact]
    public async Task GetCurrentVersionAsync_ShouldReturnNull_WhenNoCurrentVersion()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 1, false));

        // Act
        var result = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetVersionsAsync_ShouldReturnAllVersions_OrderedByVersionNumberDesc()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 1, false));
        await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 2, false));
        await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 3, true));

        // Act
        var result = (await _fixture.TokenVersionRepository.GetVersionsAsync(identityId)).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].VersionNumber); // Descending order
        Assert.Equal(2, result[1].VersionNumber);
        Assert.Equal(1, result[2].VersionNumber);
    }

    [Fact]
    public async Task GetVersionsAsync_ShouldReturnEmpty_WhenNoVersions()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();

        // Act
        var result = (await _fixture.TokenVersionRepository.GetVersionsAsync(identityId)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SetCurrentVersionAsync_ShouldSetCurrentVersion_AndUnsetOthers()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        var id1 = await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 1, true));
        var id2 = await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 2, false));
        var id3 = await _fixture.TokenVersionRepository.CreateAsync(SampleData.CreateTokenVersion(0, identityId, 3, false));

        // Act
        await _fixture.TokenVersionRepository.SetCurrentVersionAsync(identityId, id2);

        // Assert
        var version1 = await _fixture.TokenVersionRepository.GetByIdAsync(id1);
        var version2 = await _fixture.TokenVersionRepository.GetByIdAsync(id2);
        var version3 = await _fixture.TokenVersionRepository.GetByIdAsync(id3);

        Assert.False(version1!.IsCurrent);
        Assert.True(version2!.IsCurrent);
        Assert.False(version3!.IsCurrent);
    }

    [Fact]
    public async Task TokenVersion_ShouldSupportNullApiKey()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        var version = SampleData.CreateTokenVersion(0, identityId, 1);
        version.OpenAiApiKey = null;

        // Act
        var id = await _fixture.TokenVersionRepository.CreateAsync(version);
        var result = await _fixture.TokenVersionRepository.GetByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.OpenAiApiKey);
    }

    [Fact]
    public async Task TokenVersion_ShouldPersistApiKey_WhenProvided()
    {
        // Arrange
        var identityId = await CreateTestIdentityAsync();
        var version = SampleData.CreateTokenVersion(0, identityId, 1);
        version.OpenAiApiKey = "sk-test-key-123";

        // Act
        var id = await _fixture.TokenVersionRepository.CreateAsync(version);
        var result = await _fixture.TokenVersionRepository.GetByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("sk-test-key-123", result.OpenAiApiKey);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
