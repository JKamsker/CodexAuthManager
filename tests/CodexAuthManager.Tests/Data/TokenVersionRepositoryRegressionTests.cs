using System;
using System.IO;
using System.Threading.Tasks;
using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Data;

/// <summary>
/// Regression tests that reproduce previously observed issues.
/// Uses an in-memory SQLite database to isolate state.
/// </summary>
public class TokenVersionRepositoryRegressionTests : IDisposable
{
    private readonly IPathProvider _pathProvider;
    private readonly TokenDatabase _database;
    private readonly IIdentityRepository _identityRepository;
    private readonly ITokenVersionRepository _tokenVersionRepository;

    public TokenVersionRepositoryRegressionTests()
    {
        // Use a temp base path for ancillary paths; DB itself is in-memory
        var basePath = Path.Combine(Path.GetTempPath(), "CodexAuthManagerTests", Guid.NewGuid().ToString("N"));
        _pathProvider = new TestPathProvider(basePath);

        _database = new TokenDatabase(_pathProvider, useInMemory: true);
        _database.InitializeAsync().GetAwaiter().GetResult();

        _identityRepository = new IdentityRepository(_database);
        _tokenVersionRepository = new TokenVersionRepository(_database);
    }

    [Fact]
    public async Task GetCurrentVersion_ShouldPreferNewest_WhenMultipleAreMarkedCurrent()
    {
        // Arrange: create identity
        var identity = SampleData.CreateIdentity(0, "regression@example.com");
        var identityId = await _identityRepository.CreateAsync(identity);

        // Create two versions both marked as current (inconsistent state)
        var v1 = SampleData.CreateTokenVersion(0, identityId, versionNumber: 1, isCurrent: true);
        v1.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
        var v1Id = await _tokenVersionRepository.CreateAsync(v1);

        var v2 = SampleData.CreateTokenVersion(0, identityId, versionNumber: 2, isCurrent: true);
        v2.CreatedAt = DateTime.UtcNow;
        var v2Id = await _tokenVersionRepository.CreateAsync(v2);

        // Act: fetch what repository considers the "current" version
        var current = await _tokenVersionRepository.GetCurrentVersionAsync(identityId);

        // Assert: should pick the newest (highest VersionNumber)
        // This test would fail prior to the fix because selection used LIMIT 1 without ordering
        Assert.NotNull(current);
        Assert.Equal(2, current!.VersionNumber);
        Assert.Equal(v2Id, current.Id);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

