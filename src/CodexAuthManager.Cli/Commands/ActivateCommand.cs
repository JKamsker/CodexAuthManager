using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodexAuthManager.Cli.Commands;

public class ActivateSettings : CommandSettings
{
    [Description("Identity ID or email to activate")]
    [CommandArgument(0, "<identifier>")]
    public string Identifier { get; set; } = string.Empty;
}

/// <summary>
/// Handles the activate command - switches the active identity
/// </summary>
public class ActivateCommand : AsyncCommand<ActivateSettings>
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

    public override async Task<int> ExecuteAsync(CommandContext context, ActivateSettings settings)
    {
        // Find identity by ID or email
        Core.Models.Identity? identity = null;

        if (int.TryParse(settings.Identifier, out int id))
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

        // Check if token version exists before activating
        var token = await _tokenManagement.GetCurrentTokenAsync(identity.Id);
        if (token == null)
        {
            AnsiConsole.MarkupLine($"[red]No token version found for this identity:[/] {identity.Email}");
            return 1;
        }

        await AnsiConsole.Status()
            .StartAsync($"Activating identity: [bold]{identity.Email}[/]...", async ctx =>
            {
                // Create backup
                ctx.Status("Creating backup...");
                await _backupService.CreateBackupAsync();

                // Set as active in database
                ctx.Status("Updating database...");
                await _identityRepository.SetActiveAsync(identity.Id);

                // Write to auth.json
                ctx.Status("Writing auth.json...");
                _authJsonService.WriteActiveAuthToken(token);
            });

        var panel = new Panel(new Markup($@"[green]✓[/] Identity activated: [bold]{identity.Email}[/]
[dim]auth.json has been updated with the current token[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };

        AnsiConsole.Write(panel);

        return 0;
    }
}
