using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;

namespace CodexAuthManager.Cli.Commands;

/// <summary>
/// Handles the rollback command - restores a previous token version
/// </summary>
public class RollbackCommand
{
    private readonly IIdentityRepository _identityRepository;
    private readonly ITokenVersionRepository _tokenVersionRepository;
    private readonly TokenManagementService _tokenManagement;
    private readonly AuthJsonService _authJsonService;
    private readonly DatabaseBackupService _backupService;

    public RollbackCommand(
        IIdentityRepository identityRepository,
        ITokenVersionRepository tokenVersionRepository,
        TokenManagementService tokenManagement,
        AuthJsonService authJsonService,
        DatabaseBackupService backupService)
    {
        _identityRepository = identityRepository;
        _tokenVersionRepository = tokenVersionRepository;
        _tokenManagement = tokenManagement;
        _authJsonService = authJsonService;
        _backupService = backupService;
    }

    public async Task<int> ExecuteAsync(string? identifier = null, int? versionNumber = null)
    {
        // Get identity
        Core.Models.Identity? identity = null;

        if (string.IsNullOrEmpty(identifier))
        {
            identity = await _identityRepository.GetActiveIdentityAsync();
            if (identity == null)
            {
                Console.WriteLine("No active identity found. Please specify an ID or email.");
                return 1;
            }
        }
        else if (int.TryParse(identifier, out int id))
        {
            identity = await _identityRepository.GetByIdAsync(id);
        }
        else
        {
            identity = await _identityRepository.GetByEmailAsync(identifier);
        }

        if (identity == null)
        {
            Console.WriteLine($"Identity not found: {identifier}");
            return 1;
        }

        // Get versions
        var versions = (await _tokenVersionRepository.GetVersionsAsync(identity.Id)).ToList();
        if (versions.Count < 2)
        {
            Console.WriteLine("Cannot rollback: only one version exists.");
            return 1;
        }

        // Determine which version to rollback to
        Core.Models.TokenVersion? targetVersion = null;

        if (versionNumber.HasValue)
        {
            targetVersion = versions.FirstOrDefault(v => v.VersionNumber == versionNumber.Value);
            if (targetVersion == null)
            {
                Console.WriteLine($"Version {versionNumber.Value} not found.");
                return 1;
            }
        }
        else
        {
            // Default: rollback to the second most recent (previous) version
            var sortedVersions = versions.OrderByDescending(v => v.VersionNumber).ToList();
            if (sortedVersions.Count < 2)
            {
                Console.WriteLine("Cannot rollback: no previous version available.");
                return 1;
            }
            targetVersion = sortedVersions[1];
        }

        Console.WriteLine($"Rolling back identity '{identity.Email}' to version {targetVersion.VersionNumber}...");

        // Create backup
        await _backupService.CreateBackupAsync();

        // Perform rollback
        var newVersionId = await _tokenManagement.RollbackToVersionAsync(identity.Id, targetVersion.Id);

        // If this is the active identity, update auth.json
        if (identity.IsActive)
        {
            var token = await _tokenManagement.GetCurrentTokenAsync(identity.Id);
            if (token != null)
            {
                _authJsonService.WriteActiveAuthToken(token);
                Console.WriteLine("✓ auth.json has been updated");
            }
        }

        Console.WriteLine($"✓ Rolled back to version {targetVersion.VersionNumber}");
        Console.WriteLine($"  (Created new version based on v{targetVersion.VersionNumber})");

        return 0;
    }
}
