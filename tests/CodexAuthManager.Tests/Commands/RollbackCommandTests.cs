using CodexAuthManager.Cli.Commands;
using CodexAuthManager.Tests.TestHelpers;
using Spectre.Console.Cli;
using Xunit;

namespace CodexAuthManager.Tests.Commands;

[Collection("CommandTests")]
public class RollbackCommandTests : IDisposable
{
    private readonly TestFixture _fixture;
    private readonly RollbackCommand _command;

    public RollbackCommandTests()
    {
        _fixture = new TestFixture();
        _command = new RollbackCommand(
            _fixture.IdentityRepository,
            _fixture.TokenVersionRepository,
            _fixture.TokenManagement,
            _fixture.AuthJsonService,
            _fixture.BackupService);
    }

    private async Task<int> CreateIdentityWithMultipleVersionsAsync(string email)
    {
        var authToken1 = SampleData.CreateAuthToken();
        authToken1.Tokens.IdToken = SampleData.CreateSampleIdToken(email);
        authToken1.Tokens.AccessToken = "token_v1";
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.IdToken = SampleData.CreateSampleIdToken(email);
        authToken2.Tokens.AccessToken = "token_v2";
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken2);

        var authToken3 = SampleData.CreateAuthToken();
        authToken3.Tokens.IdToken = SampleData.CreateSampleIdToken(email);
        authToken3.Tokens.AccessToken = "token_v3";
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken3);

        return identityId;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRollbackToVersionNumber_WhenSpecified()
    {
        // Arrange
        var identityId = await CreateIdentityWithMultipleVersionsAsync("test@example.com");
        var settings = new RollbackSettings
        {
            Identifier = identityId.ToString(),
            Version = 1
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(0, result);

        var currentVersion = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);
        Assert.NotNull(currentVersion);
        Assert.Equal(4, currentVersion.VersionNumber); // New version based on v1
        Assert.Equal("token_v1", currentVersion.AccessToken); // Token from v1
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRollbackToPreviousVersion_WhenNotSpecified()
    {
        // Arrange
        var identityId = await CreateIdentityWithMultipleVersionsAsync("test@example.com");
        var settings = new RollbackSettings
        {
            Identifier = identityId.ToString(),
            Version = null
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(0, result);

        var currentVersion = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);
        Assert.NotNull(currentVersion);
        Assert.Equal(4, currentVersion.VersionNumber); // New version
        Assert.Equal("token_v2", currentVersion.AccessToken); // Token from v2 (previous)
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRollbackActiveIdentity_WhenNoIdentifierProvided()
    {
        // Arrange
        var identityId = await CreateIdentityWithMultipleVersionsAsync("active@example.com");
        await _fixture.IdentityRepository.SetActiveAsync(identityId);

        var settings = new RollbackSettings
        {
            Identifier = null,
            Version = 1
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(0, result);

        var currentVersion = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);
        Assert.NotNull(currentVersion);
        Assert.Equal("token_v1", currentVersion.AccessToken);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateAuthJson_WhenRollingBackActiveIdentity()
    {
        // Arrange
        var identityId = await CreateIdentityWithMultipleVersionsAsync("active@example.com");
        await _fixture.IdentityRepository.SetActiveAsync(identityId);

        var settings = new RollbackSettings
        {
            Identifier = null,
            Version = 1
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        await _command.ExecuteAsync(context, settings);

        // Assert
        var authToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.NotNull(authToken);
        Assert.Equal("token_v1", authToken.Tokens.AccessToken);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenNoActiveIdentity()
    {
        var settings = new RollbackSettings
        {
            Identifier = null,
            Version = 1
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenIdentityNotFound()
    {
        var settings = new RollbackSettings
        {
            Identifier = "nonexistent@example.com",
            Version = 1
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenVersionNotFound()
    {
        // Arrange
        var identityId = await CreateIdentityWithMultipleVersionsAsync("test@example.com");

        var settings = new RollbackSettings
        {
            Identifier = identityId.ToString(),
            Version = 999
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenOnlyOneVersion()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        var settings = new RollbackSettings
        {
            Identifier = identityId.ToString(),
            Version = null
        };
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(1, result);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
