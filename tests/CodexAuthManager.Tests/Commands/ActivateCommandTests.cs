using CodexAuthManager.Cli.Commands;
using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Commands;

public class ActivateCommandTests : IDisposable
{
    private readonly TestFixture _fixture;
    private readonly ActivateCommand _command;

    public ActivateCommandTests()
    {
        _fixture = new TestFixture();
        _command = new ActivateCommand(
            _fixture.IdentityRepository,
            _fixture.TokenManagement,
            _fixture.AuthJsonService,
            _fixture.BackupService);
    }

    private async Task<int> CreateIdentityWithVersionAsync(string email)
    {
        var authToken = SampleData.CreateAuthToken();
        authToken.Tokens.IdToken = SampleData.CreateSampleIdToken(email);
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);
        return identityId;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldActivateIdentityById()
    {
        // Arrange
        var identityId = await CreateIdentityWithVersionAsync("test@example.com");

        // Act
        var result = await _command.ExecuteAsync(identityId.ToString());

        // Assert
        Assert.Equal(0, result);

        var identity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(identity);
        Assert.True(identity.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldActivateIdentityByEmail()
    {
        // Arrange
        var identityId = await CreateIdentityWithVersionAsync("activate-me@example.com");

        // Act
        var result = await _command.ExecuteAsync("activate-me@example.com");

        // Assert
        Assert.Equal(0, result);

        var identity = await _fixture.IdentityRepository.GetByIdAsync(identityId);
        Assert.NotNull(identity);
        Assert.True(identity.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldWriteAuthJson_WhenActivating()
    {
        // Arrange
        var identityId = await CreateIdentityWithVersionAsync("test@example.com");

        // Act
        await _command.ExecuteAsync(identityId.ToString());

        // Assert
        var activeAuthPath = _fixture.PathProvider.GetActiveAuthJsonPath();
        Assert.True(_fixture.FileSystem.FileExists(activeAuthPath));

        var authToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.NotNull(authToken);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeactivateOtherIdentities()
    {
        // Arrange
        var id1 = await CreateIdentityWithVersionAsync("user1@example.com");
        var id2 = await CreateIdentityWithVersionAsync("user2@example.com");

        await _fixture.IdentityRepository.SetActiveAsync(id1);

        // Act
        await _command.ExecuteAsync(id2.ToString());

        // Assert
        var identity1 = await _fixture.IdentityRepository.GetByIdAsync(id1);
        var identity2 = await _fixture.IdentityRepository.GetByIdAsync(id2);

        Assert.False(identity1!.IsActive);
        Assert.True(identity2!.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenIdentityNotFound()
    {
        // Act
        var result = await _command.ExecuteAsync("nonexistent@example.com");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenNoTokenVersion()
    {
        // Arrange - Create identity without version
        var identity = SampleData.CreateIdentity(0, "noversion@example.com");
        var identityId = await _fixture.IdentityRepository.CreateAsync(identity);

        // Act
        var result = await _command.ExecuteAsync(identityId.ToString());

        // Assert
        Assert.Equal(1, result);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
