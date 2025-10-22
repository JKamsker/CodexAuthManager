using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodexAuthManager.Cli.Commands;

public class ImportSettings : CommandSettings
{
    [Description("Folder path to scan (defaults to Codex folder)")]
    [CommandOption("-f|--folder")]
    public string? FolderPath { get; set; }
}

/// <summary>
/// Handles the import command - scans and imports auth.json files
/// </summary>
public class ImportCommand : AsyncCommand<ImportSettings>
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

    public override async Task<int> ExecuteAsync(CommandContext context, ImportSettings settings)
    {
        AnsiConsole.MarkupLine("[bold cyan]Scanning for auth.json files...[/]");

        var authFiles = _authJsonService.ScanForAuthFiles()
            .OrderBy(file => Path.GetFileName(file).Equals("auth.json", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!authFiles.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No auth.json files found.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]Found {authFiles.Count} auth file(s):[/]");
        foreach (var file in authFiles)
        {
            AnsiConsole.MarkupLine($"  [dim]•[/] {Path.GetFileName(file)}");
        }
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[dim]Creating backup before import...[/]");
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

        await AnsiConsole.Status()
            .StartAsync("Importing...", async ctx =>
            {
                foreach (var file in authFiles)
                {
                    ctx.Status($"Processing {Path.GetFileName(file)}...");
                    try
                    {
                        var authToken = _authJsonService.ReadAuthToken(file);
                        var (identityId, versionId, isNew) = await _tokenManagement.ImportOrUpdateTokenAsync(authToken);

                        if (isNew)
                        {
                            imported++;
                            AnsiConsole.MarkupLine($"[green]✓[/] Imported new identity from {Path.GetFileName(file)}");
                        }
                        else
                        {
                            updated++;
                            AnsiConsole.MarkupLine($"[blue]✓[/] Updated identity from {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        AnsiConsole.MarkupLine($"[red]✗[/] Failed to import {Path.GetFileName(file)}: [dim]{ex.Message}[/]");
                    }
                }
            });

        AnsiConsole.WriteLine();
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Status");
        table.AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("[green]New[/]", $"[green]{imported}[/]");
        table.AddRow("[blue]Updated[/]", $"[blue]{updated}[/]");
        table.AddRow("[red]Skipped[/]", $"[red]{skipped}[/]");

        AnsiConsole.Write(table);

        return 0;
    }
}
