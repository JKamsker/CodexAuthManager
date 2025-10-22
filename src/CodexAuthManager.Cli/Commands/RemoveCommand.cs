using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;

namespace CodexAuthManager.Cli.Commands;

/// <summary>
/// Handles the remove command - deletes identities
/// </summary>
public class RemoveCommand
{
    private readonly IIdentityRepository _identityRepository;
    private readonly DatabaseBackupService _backupService;

    public RemoveCommand(
        IIdentityRepository identityRepository,
        DatabaseBackupService backupService)
    {
        _identityRepository = identityRepository;
        _backupService = backupService;
    }

    public async Task<int> ExecuteAsync(string[] identifiers)
    {
        if (!identifiers.Any())
        {
            Console.WriteLine("Please specify at least one identity ID or email to remove.");
            return 1;
        }

        Console.WriteLine("Creating backup before deletion...");
        await _backupService.CreateBackupAsync();

        int removed = 0;
        int failed = 0;

        foreach (var identifier in identifiers)
        {
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
                Console.WriteLine($"✗ Identity not found: {identifier}");
                failed++;
                continue;
            }

            try
            {
                await _identityRepository.DeleteAsync(identity.Id);
                Console.WriteLine($"✓ Removed identity: {identity.Email}");
                removed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to remove {identity.Email}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"\nRemoval complete: {removed} removed, {failed} failed");
        return failed > 0 ? 1 : 0;
    }
}
