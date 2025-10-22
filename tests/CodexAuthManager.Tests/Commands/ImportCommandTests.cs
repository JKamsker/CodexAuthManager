using CodexAuthManager.Cli.Commands;
using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Commands;

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
        authToken1.Tokens.IdToken = SampleData.CreateSampleIdToken("user1@example.com");
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/1-auth.json", authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.IdToken = SampleData.CreateSampleIdToken("user2@example.com");
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/2-auth.json", authToken2);

        // Act
        var result = await _command.ExecuteAsync();

        // Assert
        Assert.Equal(0, result);

        var identities = (await _fixture.IdentityRepository.GetAllAsync()).ToList();
        Assert.Equal(2, identities.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZero_WhenNoFilesFound()
    {
        // Act
        var result = await _command.ExecuteAsync();

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

        // Act
        await _command.ExecuteAsync();

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

        // Act
        var result = await _command.ExecuteAsync();

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
