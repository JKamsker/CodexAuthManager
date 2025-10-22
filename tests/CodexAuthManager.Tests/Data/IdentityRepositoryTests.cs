using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Data;

public class IdentityRepositoryTests : IDisposable
{
    private readonly TestFixture _fixture;

    public IdentityRepositoryTests()
    {
        _fixture = new TestFixture();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateIdentity_AndReturnId()
    {
        // Arrange
        var identity = SampleData.CreateIdentity(id: 0, email: "new@example.com");

        // Act
        var id = await _fixture.IdentityRepository.CreateAsync(identity);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnIdentity_WhenExists()
    {
        // Arrange
        var identity = SampleData.CreateIdentity(id: 0, email: "test@example.com");
        var id = await _fixture.IdentityRepository.CreateAsync(identity);

        // Act
        var result = await _fixture.IdentityRepository.GetByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("test@example.com", result.Email);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _fixture.IdentityRepository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnIdentity_WhenExists()
    {
        // Arrange
        var identity = SampleData.CreateIdentity(id: 0, email: "findme@example.com");
        await _fixture.IdentityRepository.CreateAsync(identity);

        // Act
        var result = await _fixture.IdentityRepository.GetByEmailAsync("findme@example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("findme@example.com", result.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _fixture.IdentityRepository.GetByEmailAsync("nonexistent@example.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllIdentities()
    {
        // Arrange
        await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user1@example.com"));
        await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user2@example.com"));
        await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user3@example.com"));

        // Act
        var result = (await _fixture.IdentityRepository.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenNoIdentities()
    {
        // Act
        var result = (await _fixture.IdentityRepository.GetAllAsync()).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateIdentity()
    {
        // Arrange
        var identity = SampleData.CreateIdentity(0, "original@example.com");
        var id = await _fixture.IdentityRepository.CreateAsync(identity);
        identity.Id = id;
        identity.Email = "updated@example.com";
        identity.PlanType = "free";

        // Act
        await _fixture.IdentityRepository.UpdateAsync(identity);

        // Assert
        var result = await _fixture.IdentityRepository.GetByIdAsync(id);
        Assert.NotNull(result);
        Assert.Equal("updated@example.com", result.Email);
        Assert.Equal("free", result.PlanType);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveIdentity()
    {
        // Arrange
        var identity = SampleData.CreateIdentity(0, "delete@example.com");
        var id = await _fixture.IdentityRepository.CreateAsync(identity);

        // Act
        await _fixture.IdentityRepository.DeleteAsync(id);

        // Assert
        var result = await _fixture.IdentityRepository.GetByIdAsync(id);
        Assert.Null(result);
    }

    [Fact]
    public async Task SetActiveAsync_ShouldActivateIdentity_AndDeactivateOthers()
    {
        // Arrange
        var id1 = await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user1@example.com", isActive: true));
        var id2 = await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user2@example.com", isActive: false));
        var id3 = await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "user3@example.com", isActive: false));

        // Act
        await _fixture.IdentityRepository.SetActiveAsync(id2);

        // Assert
        var identity1 = await _fixture.IdentityRepository.GetByIdAsync(id1);
        var identity2 = await _fixture.IdentityRepository.GetByIdAsync(id2);
        var identity3 = await _fixture.IdentityRepository.GetByIdAsync(id3);

        Assert.False(identity1!.IsActive);
        Assert.True(identity2!.IsActive);
        Assert.False(identity3!.IsActive);
    }

    [Fact]
    public async Task GetActiveIdentityAsync_ShouldReturnActiveIdentity()
    {
        // Arrange
        await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "inactive@example.com", isActive: false));
        var activeId = await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "active@example.com", isActive: true));

        // Act
        var result = await _fixture.IdentityRepository.GetActiveIdentityAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(activeId, result.Id);
        Assert.Equal("active@example.com", result.Email);
    }

    [Fact]
    public async Task GetActiveIdentityAsync_ShouldReturnNull_WhenNoActiveIdentity()
    {
        // Arrange
        await _fixture.IdentityRepository.CreateAsync(SampleData.CreateIdentity(0, "inactive@example.com", isActive: false));

        // Act
        var result = await _fixture.IdentityRepository.GetActiveIdentityAsync();

        // Assert
        Assert.Null(result);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
