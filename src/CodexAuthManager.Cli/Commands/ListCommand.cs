using CodexAuthManager.Core.Data;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CodexAuthManager.Cli.Commands;

public class ListSettings : CommandSettings
{
}

/// <summary>
/// Handles the list command - displays all identities
/// </summary>
public class ListCommand : AsyncCommand<ListSettings>
{
    private readonly IIdentityRepository _identityRepository;

    public ListCommand(IIdentityRepository identityRepository)
    {
        _identityRepository = identityRepository;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        var identities = (await _identityRepository.GetAllAsync()).ToList();

        if (!identities.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No identities found.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold cyan]Found {identities.Count} {(identities.Count == 1 ? "identity" : "identities")}:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]ID[/]").RightAligned());
        table.AddColumn("[bold]Email[/]");
        table.AddColumn("[bold]Plan[/]");
        table.AddColumn(new TableColumn("[bold]Active[/]").Centered());
        table.AddColumn("[bold]Last Updated[/]");

        foreach (var identity in identities)
        {
            var activeMarker = identity.IsActive ? "[green]✓[/]" : "";
            var updated = identity.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var emailColor = identity.IsActive ? "[bold green]" : "";
            var emailMarkup = identity.IsActive ? $"{emailColor}{identity.Email}[/]" : identity.Email;

            table.AddRow(
                identity.Id.ToString(),
                emailMarkup,
                identity.PlanType,
                activeMarker,
                $"[dim]{updated}[/]"
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
