using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;

namespace CodexAuthManager.Cli.Commands;

/// <summary>
/// Handles the activate command - switches the active identity
/// </summary>
public class ActivateCommand
{
    private readonly IIdentityRepository _identityRepository;
    private readonly TokenManagementService _tokenManagement;
    private readonly AuthJsonService _authJsonService;
    private readonly DatabaseBackupService _backupService;

    public ActivateCommand(
        IIdentityRepository identityRepository,
        TokenManagementService tokenManagement,
        AuthJsonService authJsonService,
        DatabaseBackupService backupService)
    {
        _identityRepository = identityRepository;
        _tokenManagement = tokenManagement;
        _authJsonService = authJsonService;
        _backupService = backupService;
    }

    public async Task<int> ExecuteAsync(string identifier)
    {
        // Find identity by ID or email
        Core.Models.Identity? identity = null;

        if (int.TryParse(identifier, out int id))
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

        Console.WriteLine($"Activating identity: {identity.Email}...");

        // Create backup
        await _backupService.CreateBackupAsync();

        // Set as active in database
        await _identityRepository.SetActiveAsync(identity.Id);

        // Get current token and write to auth.json
        var token = await _tokenManagement.GetCurrentTokenAsync(identity.Id);
        if (token == null)
        {
            Console.WriteLine("Error: No token version found for this identity");
            return 1;
        }

        _authJsonService.WriteActiveAuthToken(token);

        Console.WriteLine($"✓ Identity activated: {identity.Email}");
        Console.WriteLine($"  auth.json has been updated with the current token");

        return 0;
    }
}
