using CodexAuthManager.Cli.Commands;
using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Commands;

public class ListCommandTests : IDisposable
{
    private readonly TestFixture _fixture;
    private readonly ListCommand _command;

    public ListCommandTests()
    {
        _fixture = new TestFixture();
        _command = new ListCommand(_fixture.IdentityRepository);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZero_WhenNoIdentities()
    {
        // Act
        var result = await _command.ExecuteAsync();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZero_WhenIdentitiesExist()
    {
        // Arrange
        await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user1@example.com"));
        await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user2@example.com"));

        // Act
        var result = await _command.ExecuteAsync();

        // Assert
        Assert.Equal(0, result);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
