using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodexAuthManager.Cli.Commands;

public class ShowSettings : CommandSettings
{
    [Description("Identity ID, email, or leave empty for active identity")]
    [CommandArgument(0, "[identifier]")]
    public string? Identifier { get; set; }
}

/// <summary>
/// Handles the show command - displays identity details
/// </summary>
public class ShowCommand : AsyncCommand<ShowSettings>
{
    private readonly IIdentityRepository _identityRepository;
    private readonly ITokenVersionRepository _tokenVersionRepository;
    private readonly AuthJsonService _authJsonService;

    public ShowCommand(
        IIdentityRepository identityRepository,
        ITokenVersionRepository tokenVersionRepository,
        AuthJsonService authJsonService)
    {
        _identityRepository = identityRepository;
        _tokenVersionRepository = tokenVersionRepository;
        _authJsonService = authJsonService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ShowSettings settings)
    {
        // Get identity - either by ID, email, or active one
        Core.Models.Identity? identity = null;

        if (string.IsNullOrEmpty(settings.Identifier))
        {
            // Try to get identity from active auth.json
            var activeAuth = _authJsonService.ReadActiveAuthToken();
            if (activeAuth != null && !string.IsNullOrEmpty(activeAuth.Tokens.AccountId))
            {
                identity = await _identityRepository.GetByAccountIdAsync(activeAuth.Tokens.AccountId);
            }

            // Fall back to database active identity
            if (identity == null)
            {
                identity = await _identityRepository.GetActiveIdentityAsync();
            }

            if (identity == null)
            {
                AnsiConsole.MarkupLine("[red]No active identity found. Please specify an ID or email.[/]");
                return 1;
            }
        }
        else if (int.TryParse(settings.Identifier, out int id))
        {
            identity = await _identityRepository.GetByIdAsync(id);
        }
        else
        {
            identity = await _identityRepository.GetByEmailAsync(settings.Identifier);
        }

        if (identity == null)
        {
            AnsiConsole.MarkupLine($"[red]Identity not found:[/] {settings.Identifier}");
            return 1;
        }

        // Display identity details in a panel
        var panel = new Panel(new Markup($@"[bold]Email:[/]          {identity.Email}
[bold]User ID:[/]        {identity.UserId}
[bold]Account ID:[/]     {identity.AccountId}
[bold]Plan Type:[/]      {identity.PlanType}
[bold]Active:[/]         {(identity.IsActive ? "[green]Yes[/]" : "[dim]No[/]")}
[bold]Created:[/]        [dim]{identity.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}[/]
[bold]Last Updated:[/]   [dim]{identity.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}[/]"))
        {
            Header = new PanelHeader($"[bold cyan]Identity #{identity.Id}[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Display token versions
        var versions = (await _tokenVersionRepository.GetVersionsAsync(identity.Id)).ToList();
        AnsiConsole.MarkupLine($"[bold cyan]Token Versions ([/][bold]{versions.Count}[/][bold cyan]):[/]");

        if (versions.Any())
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[bold]Version[/]").RightAligned());
            table.AddColumn("[bold]Created[/]");
            table.AddColumn("[bold]Last Refresh[/]");
            table.AddColumn(new TableColumn("[bold]Current[/]").Centered());

            foreach (var version in versions)
            {
                var currentMarker = version.IsCurrent ? "[green]✓[/]" : "";
                var created = version.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                var lastRefresh = version.LastRefresh.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                table.AddRow(
                    $"v{version.VersionNumber}",
                    $"[dim]{created}[/]",
                    $"[dim]{lastRefresh}[/]",
                    currentMarker
                );
            }

            AnsiConsole.Write(table);
        }

        return 0;
    }
}
