using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodexAuthManager.Cli.Commands;

public class RemoveSettings : CommandSettings
{
    [Description("One or more identity IDs or emails to remove")]
    [CommandArgument(0, "<identifiers>")]
    public string[] Identifiers { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Handles the remove command - deletes identities
/// </summary>
public class RemoveCommand : AsyncCommand<RemoveSettings>
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

    public override async Task<int> ExecuteAsync(CommandContext context, RemoveSettings settings)
    {
        if (!settings.Identifiers.Any())
        {
            AnsiConsole.MarkupLine("[red]Please specify at least one identity ID or email to remove.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[dim]Creating backup before deletion...[/]");
        await _backupService.CreateBackupAsync();
        AnsiConsole.WriteLine();

        int removed = 0;
        int failed = 0;

        foreach (var identifier in settings.Identifiers)
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
                AnsiConsole.MarkupLine($"[red]✗[/] Identity not found: [dim]{identifier}[/]");
                failed++;
                continue;
            }

            try
            {
                await _identityRepository.DeleteAsync(identity.Id);
                AnsiConsole.MarkupLine($"[green]✓[/] Removed identity: [bold]{identity.Email}[/]");
                removed++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to remove {identity.Email}: [dim]{ex.Message}[/]");
                failed++;
            }
        }

        AnsiConsole.WriteLine();
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Status");
        table.AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("[green]Removed[/]", $"[green]{removed}[/]");
        table.AddRow("[red]Failed[/]", $"[red]{failed}[/]");

        AnsiConsole.Write(table);

        return failed > 0 ? 1 : 0;
    }
}
