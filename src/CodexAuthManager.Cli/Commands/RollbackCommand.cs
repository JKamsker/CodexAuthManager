using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodexAuthManager.Cli.Commands;

public class RollbackSettings : CommandSettings
{
    [Description("Identity ID, email, or leave empty for active identity")]
    [CommandArgument(0, "[identifier]")]
    public string? Identifier { get; set; }

    [Description("Version number to rollback to (defaults to previous version)")]
    [CommandOption("-v|--version")]
    public int? Version { get; set; }
}

/// <summary>
/// Handles the rollback command - restores a previous token version
/// </summary>
public class RollbackCommand : AsyncCommand<RollbackSettings>
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

    public override async Task<int> ExecuteAsync(CommandContext context, RollbackSettings settings)
    {
        // Get identity
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

        // Get versions
        var versions = (await _tokenVersionRepository.GetVersionsAsync(identity.Id)).ToList();
        if (versions.Count < 2)
        {
            AnsiConsole.MarkupLine("[yellow]Cannot rollback: only one version exists.[/]");
            return 1;
        }

        // Determine which version to rollback to
        Core.Models.TokenVersion? targetVersion = null;

        if (settings.Version.HasValue)
        {
            targetVersion = versions.FirstOrDefault(v => v.VersionNumber == settings.Version.Value);
            if (targetVersion == null)
            {
                AnsiConsole.MarkupLine($"[red]Version {settings.Version.Value} not found.[/]");
                return 1;
            }
        }
        else
        {
            // Default: rollback to the second most recent (previous) version
            var sortedVersions = versions.OrderByDescending(v => v.VersionNumber).ToList();
            if (sortedVersions.Count < 2)
            {
                AnsiConsole.MarkupLine("[yellow]Cannot rollback: no previous version available.[/]");
                return 1;
            }
            targetVersion = sortedVersions[1];
        }

        int newVersionNumber = 0;

        await AnsiConsole.Status()
            .StartAsync($"Rolling back identity '[bold]{identity.Email}[/]' to version {targetVersion.VersionNumber}...", async ctx =>
            {
                // Create backup
                ctx.Status("Creating backup...");
                await _backupService.CreateBackupAsync();

                // Perform rollback
                ctx.Status("Creating new version...");
                var newVersionId = await _tokenManagement.RollbackToVersionAsync(identity.Id, targetVersion.Id);
                var newVersion = await _tokenVersionRepository.GetByIdAsync(newVersionId);
                newVersionNumber = newVersion!.VersionNumber;

                // If this is the active identity, update auth.json
                if (identity.IsActive)
                {
                    ctx.Status("Updating auth.json...");
                    var token = await _tokenManagement.GetCurrentTokenAsync(identity.Id);
                    if (token != null)
                    {
                        _authJsonService.WriteActiveAuthToken(token);
                    }
                }
            });

        var panel = new Panel(new Markup($@"[green]✓[/] Rolled back to version {targetVersion.VersionNumber}
[dim]Created new version v{newVersionNumber} based on v{targetVersion.VersionNumber}[/]
{(identity.IsActive ? "[dim]auth.json has been updated[/]" : "")}"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };

        AnsiConsole.Write(panel);

        return 0;
    }
}
