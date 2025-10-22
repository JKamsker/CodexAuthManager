using CodexAuthManager.Tests.TestHelpers;
using System.IO;
using System.Text.Json;
using Xunit;

namespace CodexAuthManager.Tests.Services;

public class AuthJsonServiceTests : IDisposable
{
    private readonly TestFixture _fixture;

    public AuthJsonServiceTests()
    {
        _fixture = new TestFixture();
    }

    [Fact]
    public void ScanForAuthFiles_ShouldReturnAllAuthJsonFiles()
    {
        // Arrange
        var codexFolder = _fixture.PathProvider.GetCodexFolderPath();
        _fixture.FileSystem.WriteAllText($"{codexFolder}/auth.json", "{}");
        _fixture.FileSystem.WriteAllText($"{codexFolder}/1-test-auth.json", "{}");
        _fixture.FileSystem.WriteAllText($"{codexFolder}/2-demo-auth.json", "{}");
        _fixture.FileSystem.WriteAllText($"{codexFolder}/config.toml", "test");

        // Act
        var files = _fixture.AuthJsonService.ScanForAuthFiles().ToList();

        // Assert
        Assert.Equal(3, files.Count);
        Assert.All(files, f => Assert.EndsWith("auth.json", f));
    }

    [Fact]
    public void ScanForAuthFiles_ShouldReturnEmpty_WhenNoFiles()
    {
        // Act
        var files = _fixture.AuthJsonService.ScanForAuthFiles().ToList();

        // Assert
        Assert.Empty(files);
    }

    [Fact]
    public void WriteAuthToken_ShouldCreateValidJsonFile()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        var filePath = $"{_fixture.PathProvider.GetCodexFolderPath()}/test-auth.json";

        // Act
        _fixture.AuthJsonService.WriteAuthToken(filePath, authToken);

        // Assert
        Assert.True(_fixture.FileSystem.FileExists(filePath));
        var content = _fixture.FileSystem.ReadAllText(filePath);
        Assert.NotEmpty(content);

        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(content);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void ReadAuthToken_ShouldDeserializeValidAuthJson()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        var filePath = $"{_fixture.PathProvider.GetCodexFolderPath()}/test-auth.json";
        _fixture.AuthJsonService.WriteAuthToken(filePath, authToken);

        // Act
        var result = _fixture.AuthJsonService.ReadAuthToken(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(authToken.Tokens.AccessToken, result.Tokens.AccessToken);
        Assert.Equal(authToken.Tokens.RefreshToken, result.Tokens.RefreshToken);
        Assert.Equal(authToken.Tokens.AccountId, result.Tokens.AccountId);
    }

    [Fact]
    public void WriteActiveAuthToken_ShouldWriteToCorrectLocation()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();

        // Act
        _fixture.AuthJsonService.WriteActiveAuthToken(authToken);

        // Assert
        var activeAuthPath = _fixture.PathProvider.GetActiveAuthJsonPath();
        Assert.True(_fixture.FileSystem.FileExists(activeAuthPath));
    }

    [Fact]
    public void ReadActiveAuthToken_ShouldReturnNull_WhenFileNotExists()
    {
        // Act
        var result = _fixture.AuthJsonService.ReadActiveAuthToken();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ReadActiveAuthToken_ShouldReturnToken_WhenFileExists()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        _fixture.AuthJsonService.WriteActiveAuthToken(authToken);

        // Act
        var result = _fixture.AuthJsonService.ReadActiveAuthToken();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(authToken.Tokens.AccessToken, result.Tokens.AccessToken);
    }

    [Fact]
    public void WriteAuthToken_ShouldHandleNullApiKey()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        authToken.OpenAiApiKey = null;
        var filePath = $"{_fixture.PathProvider.GetCodexFolderPath()}/test-auth.json";

        // Act
        _fixture.AuthJsonService.WriteAuthToken(filePath, authToken);
        var result = _fixture.AuthJsonService.ReadAuthToken(filePath);

        // Assert
        Assert.Null(result.OpenAiApiKey);
    }

    [Fact]
    public void WriteAuthToken_ShouldPreserveApiKey_WhenProvided()
    {
        // Arrange
        var authToken = SampleData.CreateAuthToken();
        authToken.OpenAiApiKey = "sk-test-key";
        var filePath = $"{_fixture.PathProvider.GetCodexFolderPath()}/test-auth.json";

        // Act
        _fixture.AuthJsonService.WriteAuthToken(filePath, authToken);
        var result = _fixture.AuthJsonService.ReadAuthToken(filePath);

        // Assert
        Assert.Equal("sk-test-key", result.OpenAiApiKey);
    }

    [Fact]
    public void ReadAuthToken_ShouldThrow_WhenFileNotFound()
    {
        // Arrange
        var missingPath = Path.Combine(_fixture.PathProvider.GetCodexFolderPath(), "missing-auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(missingPath)!);

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            _fixture.AuthJsonService.ReadAuthToken(missingPath));
    }

    [Fact]
    public void WriteAuthToken_ShouldOverwriteExistingFile()
    {
        // Arrange
        var authToken1 = SampleData.CreateAuthToken();
        authToken1.Tokens.AccessToken = "token1";
        var filePath = $"{_fixture.PathProvider.GetCodexFolderPath()}/test-auth.json";

        _fixture.AuthJsonService.WriteAuthToken(filePath, authToken1);

        var authToken2 = SampleData.CreateAuthToken();
        authToken2.Tokens.AccessToken = "token2";

        // Act
        _fixture.AuthJsonService.WriteAuthToken(filePath, authToken2);
        var result = _fixture.AuthJsonService.ReadAuthToken(filePath);

        // Assert
        Assert.Equal("token2", result.Tokens.AccessToken);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
