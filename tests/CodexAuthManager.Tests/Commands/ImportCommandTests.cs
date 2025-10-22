using CodexAuthManager.Cli.Commands;
using CodexAuthManager.Tests.TestHelpers;
using Spectre.Console.Cli;
using Xunit;

namespace CodexAuthManager.Tests.Commands;

[Collection("CommandTests")]
public class ImportCommandTests : IDisposable
{
    private readonly TestFixture _fixture;
    private readonly ImportCommand _command;

    public ImportCommandTests()
    {
        _fixture = new TestFixture();
        _command = new ImportCommand(
            _fixture.AuthJsonService,
            _fixture.TokenManagement,
            _fixture.BackupService);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldImportAllAuthJsonFiles()
    {
        // Arrange
        var codexFolder = _fixture.PathProvider.GetCodexFolderPath();
        var authToken1 = SampleData.CreateAuthToken();
        authToken1.Tokens.IdToken = SampleData.CreateSampleIdToken("user1@example.com", accountId: "account-1");
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/1-auth.json", authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.IdToken = SampleData.CreateSampleIdToken("user2@example.com", accountId: "account-2");
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/2-auth.json", authToken2);

        var settings = new ImportSettings();
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "import", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(0, result);

        var identities = (await _fixture.IdentityRepository.GetAllAsync()).ToList();
        Assert.Equal(2, identities.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZero_WhenNoFilesFound()
    {
        var settings = new ImportSettings();
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "import", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateVersions_ForEachImportedIdentity()
    {
        // Arrange
        var codexFolder = _fixture.PathProvider.GetCodexFolderPath();
        var authToken = SampleData.CreateAuthToken();
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/auth.json", authToken);

        var settings = new ImportSettings();
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "import", null);

        // Act
        await _command.ExecuteAsync(context, settings);

        // Assert
        var identities = (await _fixture.IdentityRepository.GetAllAsync()).ToList();
        Assert.Single(identities);

        var versions = await _fixture.TokenVersionRepository.GetVersionsAsync(identities[0].Id);
        Assert.Single(versions);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleInvalidFiles_Gracefully()
    {
        // Arrange
        var codexFolder = _fixture.PathProvider.GetCodexFolderPath();
        _fixture.FileSystem.WriteAllText($"{codexFolder}/invalid-auth.json", "invalid json content");

        var validToken = SampleData.CreateAuthToken();
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/valid-auth.json", validToken);

        var settings = new ImportSettings();
        var context = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "import", null);

        // Act
        var result = await _command.ExecuteAsync(context, settings);

        // Assert
        Assert.Equal(0, result);

        var identities = (await _fixture.IdentityRepository.GetAllAsync()).ToList();
        Assert.Single(identities); // Only valid file imported
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
