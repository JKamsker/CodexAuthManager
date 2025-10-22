using CodexAuthManager.Core.Services;

namespace CodexAuthManager.Cli.Commands;

/// <summary>
/// Handles the import command - scans and imports auth.json files
/// </summary>
public class ImportCommand
{
    private readonly AuthJsonService _authJsonService;
    private readonly TokenManagementService _tokenManagement;
    private readonly DatabaseBackupService _backupService;

    public ImportCommand(
        AuthJsonService authJsonService,
        TokenManagementService tokenManagement,
        DatabaseBackupService backupService)
    {
        _authJsonService = authJsonService;
        _tokenManagement = tokenManagement;
        _backupService = backupService;
    }

    public async Task<int> ExecuteAsync(string? folderPath = null)
    {
        Console.WriteLine("Scanning for auth.json files...");

        var authFiles = _authJsonService.ScanForAuthFiles().ToList();
        if (!authFiles.Any())
        {
            Console.WriteLine("No auth.json files found.");
            return 0;
        }

        Console.WriteLine($"Found {authFiles.Count} auth file(s):");
        foreach (var file in authFiles)
        {
            Console.WriteLine($"  - {Path.GetFileName(file)}");
        }

        Console.WriteLine("\nCreating backup before import...");
        try
        {
            await _backupService.CreateBackupAsync();
        }
        catch
        {
            // Backup might fail if no database exists yet, that's okay for initial import
        }

        int imported = 0;
        int updated = 0;
        int skipped = 0;

        foreach (var file in authFiles)
        {
            try
            {
                var authToken = _authJsonService.ReadAuthToken(file);
                var (identityId, versionId, isNew) = await _tokenManagement.ImportOrUpdateTokenAsync(authToken);

                if (isNew)
                {
                    imported++;
                    Console.WriteLine($"✓ Imported new identity from {Path.GetFileName(file)}");
                }
                else
                {
                    updated++;
                    Console.WriteLine($"✓ Updated identity from {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                skipped++;
                Console.WriteLine($"✗ Failed to import {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nImport complete: {imported} new, {updated} updated, {skipped} skipped");
        return 0;
    }
}
