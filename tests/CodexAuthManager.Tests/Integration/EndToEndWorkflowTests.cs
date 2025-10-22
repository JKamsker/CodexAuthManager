using CodexAuthManager.Cli.Commands;
using CodexAuthManager.Tests.TestHelpers;
using Spectre.Console.Cli;
using Xunit;

namespace CodexAuthManager.Tests.Integration;

/// <summary>
/// End-to-end integration tests simulating complete user workflows
/// </summary>
[Collection("CommandTests")]
public class EndToEndWorkflowTests : IDisposable
{
    private readonly TestFixture _fixture;

    public EndToEndWorkflowTests()
    {
        _fixture = new TestFixture();
    }

    [Fact]
    public async Task CompleteWorkflow_ImportListActivateRollback_ShouldWorkCorrectly()
    {
        // Arrange - Create auth files in the codex folder
        var codexFolder = _fixture.PathProvider.GetCodexFolderPath();

        var user1Token = SampleData.CreateAuthToken();
        user1Token.Tokens.IdToken = SampleData.CreateSampleIdToken("user1@example.com");
        user1Token.Tokens.AccessToken = "user1_token";
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/1-auth.json", user1Token);

        var user2Token = SampleData.CreateAuthToken();
        user2Token.Tokens.IdToken = SampleData.CreateSampleIdToken("user2@example.com");
        user2Token.Tokens.AccessToken = "user2_token";
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/2-auth.json", user2Token);

        // Step 1: Import
        var importCmd = new ImportCommand(_fixture.AuthJsonService, _fixture.TokenManagement, _fixture.BackupService);
        var importSettings = new ImportSettings();
        var importContext = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "import", null);
        var importResult = await importCmd.ExecuteAsync(importContext, importSettings);
        Assert.Equal(0, importResult);

        // Verify import
        var identities = (await _fixture.IdentityRepository.GetAllAsync()).ToList();
        Assert.Equal(2, identities.Count);

        // Step 2: List
        var listCmd = new ListCommand(_fixture.IdentityRepository);
        var listSettings = new ListSettings();
        var listContext = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "list", null);
        var listResult = await listCmd.ExecuteAsync(listContext, listSettings);
        Assert.Equal(0, listResult);

        // Step 3: Activate user1
        var activateCmd = new ActivateCommand(
            _fixture.IdentityRepository,
            _fixture.TokenManagement,
            _fixture.AuthJsonService,
            _fixture.BackupService);

        var activateSettings = new ActivateSettings { Identifier = "user1@example.com" };
        var activateContext = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "activate", null);
        var activateResult = await activateCmd.ExecuteAsync(activateContext, activateSettings);
        Assert.Equal(0, activateResult);

        // Verify activation
        var activeIdentity = await _fixture.IdentityRepository.GetActiveIdentityAsync();
        Assert.NotNull(activeIdentity);
        Assert.Equal("user1@example.com", activeIdentity.Email);

        var activeAuthToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.NotNull(activeAuthToken);
        Assert.Equal("user1_token", activeAuthToken.Tokens.AccessToken);

        // Step 4: Update user1's token (simulate refresh)
        var updatedUser1Token = SampleData.CreateAuthToken();
        updatedUser1Token.Tokens.IdToken = SampleData.CreateSampleIdToken("user1@example.com");
        updatedUser1Token.Tokens.AccessToken = "user1_token_v2";
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/1-auth.json", updatedUser1Token);

        // Re-import
        await importCmd.ExecuteAsync(importContext, importSettings);

        // Verify new version created
        var user1Identity = await _fixture.IdentityRepository.GetByEmailAsync("user1@example.com");
        Assert.NotNull(user1Identity);

        var versions = (await _fixture.TokenVersionRepository.GetVersionsAsync(user1Identity.Id)).ToList();
        Assert.Equal(2, versions.Count);
        Assert.Equal(2, versions[0].VersionNumber); // Latest
        Assert.Equal("user1_token_v2", versions[0].AccessToken);

        // Step 5: Rollback to previous version
        var rollbackCmd = new RollbackCommand(
            _fixture.IdentityRepository,
            _fixture.TokenVersionRepository,
            _fixture.TokenManagement,
            _fixture.AuthJsonService,
            _fixture.BackupService);

        var rollbackSettings = new RollbackSettings { Identifier = "user1@example.com", Version = 1 };
        var rollbackContext = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "rollback", null);
        var rollbackResult = await rollbackCmd.ExecuteAsync(rollbackContext, rollbackSettings);
        Assert.Equal(0, rollbackResult);

        // Verify rollback
        var currentVersion = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(user1Identity.Id);
        Assert.NotNull(currentVersion);
        Assert.Equal(3, currentVersion.VersionNumber); // New version based on v1
        Assert.Equal("user1_token", currentVersion.AccessToken); // Original token

        // Verify auth.json updated (since user1 is active)
        activeAuthToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.NotNull(activeAuthToken);
        Assert.Equal("user1_token", activeAuthToken.Tokens.AccessToken);

        // Step 6: Switch to user2
        activateSettings.Identifier = "user2@example.com";
        await activateCmd.ExecuteAsync(activateContext, activateSettings);

        activeIdentity = await _fixture.IdentityRepository.GetActiveIdentityAsync();
        Assert.NotNull(activeIdentity);
        Assert.Equal("user2@example.com", activeIdentity.Email);

        activeAuthToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.NotNull(activeAuthToken);
        Assert.Equal("user2_token", activeAuthToken.Tokens.AccessToken);
    }

    [Fact]
    public async Task MultipleImports_ShouldCreateVersions_OnlyWhenTokensChange()
    {
        // Arrange
        var codexFolder = _fixture.PathProvider.GetCodexFolderPath();
        var authToken = SampleData.CreateAuthToken();
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/auth.json", authToken);

        var importCmd = new ImportCommand(_fixture.AuthJsonService, _fixture.TokenManagement, _fixture.BackupService);
        var importSettings = new ImportSettings();
        var importContext = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "import", null);

        // Act - Import same token 3 times
        await importCmd.ExecuteAsync(importContext, importSettings);
        await importCmd.ExecuteAsync(importContext, importSettings);
        await importCmd.ExecuteAsync(importContext, importSettings);

        // Assert - Only 1 version should exist
        var identities = (await _fixture.IdentityRepository.GetAllAsync()).ToList();
        Assert.Single(identities);

        var versions = (await _fixture.TokenVersionRepository.GetVersionsAsync(identities[0].Id)).ToList();
        Assert.Single(versions);

        // Now change the token and import again
        authToken.Tokens.AccessToken = "new_token";
        _fixture.AuthJsonService.WriteAuthToken($"{codexFolder}/auth.json", authToken);
        await importCmd.ExecuteAsync(importContext, importSettings);

        // Assert - Now 2 versions should exist
        versions = (await _fixture.TokenVersionRepository.GetVersionsAsync(identities[0].Id)).ToList();
        Assert.Equal(2, versions.Count);
    }

    [Fact]
    public async Task RollbackSequence_ShouldMaintainVersionHistory()
    {
        // Arrange - Create identity with 3 versions
        var authToken = SampleData.CreateAuthToken();
        var (identityId, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        authToken.Tokens.AccessToken = "v2";
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        authToken.Tokens.AccessToken = "v3";
        await _fixture.TokenManagement.ImportOrUpdateTokenAsync(authToken);

        // Current state: v1, v2, v3 (current)

        // Rollback to v1
        await _fixture.TokenManagement.RollbackToVersionAsync(identityId,
            (await _fixture.TokenVersionRepository.GetVersionsAsync(identityId))
                .First(v => v.VersionNumber == 1).Id);

        // Current state: v1, v2, v3, v4 (current, based on v1)

        // Rollback to v2
        await _fixture.TokenManagement.RollbackToVersionAsync(identityId,
            (await _fixture.TokenVersionRepository.GetVersionsAsync(identityId))
                .First(v => v.VersionNumber == 2).Id);

        // Current state: v1, v2, v3, v4, v5 (current, based on v2)

        // Assert - All versions preserved
        var versions = (await _fixture.TokenVersionRepository.GetVersionsAsync(identityId)).ToList();
        Assert.Equal(5, versions.Count);

        // Verify current version
        var currentVersion = await _fixture.TokenVersionRepository.GetCurrentVersionAsync(identityId);
        Assert.NotNull(currentVersion);
        Assert.Equal(5, currentVersion.VersionNumber);
        Assert.Equal("v2", currentVersion.AccessToken);

        // Verify all versions still accessible
        Assert.Contains(versions, v => v.VersionNumber == 1);
        Assert.Contains(versions, v => v.VersionNumber == 2);
        Assert.Contains(versions, v => v.VersionNumber == 3);
        Assert.Contains(versions, v => v.VersionNumber == 4);
        Assert.Contains(versions, v => v.VersionNumber == 5);
    }

    [Fact]
    public async Task ActivateDifferentIdentities_ShouldUpdateAuthJson_Correctly()
    {
        // Arrange
        var identity1Token = SampleData.CreateAuthToken();
        identity1Token.Tokens.IdToken = SampleData.CreateSampleIdToken("identity1@example.com");
        identity1Token.Tokens.AccessToken = "token1";
        var (id1, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(identity1Token);

        var identity2Token = SampleData.CreateAuthToken();
        identity2Token.Tokens.IdToken = SampleData.CreateSampleIdToken("identity2@example.com");
        identity2Token.Tokens.AccessToken = "token2";
        var (id2, _, _) = await _fixture.TokenManagement.ImportOrUpdateTokenAsync(identity2Token);

        var activateCmd = new ActivateCommand(
            _fixture.IdentityRepository,
            _fixture.TokenManagement,
            _fixture.AuthJsonService,
            _fixture.BackupService);

        var activateSettings = new ActivateSettings();
        var activateContext = new CommandContext(Array.Empty<string>(), new TestRemainingArguments(), "activate", null);

        // Activate identity1
        activateSettings.Identifier = id1.ToString();
        await activateCmd.ExecuteAsync(activateContext, activateSettings);
        var authToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.Equal("token1", authToken!.Tokens.AccessToken);

        // Activate identity2
        activateSettings.Identifier = id2.ToString();
        await activateCmd.ExecuteAsync(activateContext, activateSettings);
        authToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.Equal("token2", authToken!.Tokens.AccessToken);

        // Activate identity1 again
        activateSettings.Identifier = id1.ToString();
        await activateCmd.ExecuteAsync(activateContext, activateSettings);
        authToken = _fixture.AuthJsonService.ReadActiveAuthToken();
        Assert.Equal("token1", authToken!.Tokens.AccessToken);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
