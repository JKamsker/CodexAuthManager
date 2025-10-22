using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodexAuthManager.Cli.Commands;

public class StatsSettings : CommandSettings
{
    [Description("Identity ID, email, or leave empty for active identity")]
    [CommandArgument(0, "[identifier]")]
    public string? Identifier { get; set; }
}

/// <summary>
/// Handles the stats command - retrieves and saves Codex usage statistics
/// </summary>
public class StatsCommand : AsyncCommand<StatsSettings>
{
    private readonly IIdentityRepository _identityRepository;
    private readonly IUsageStatsRepository _usageStatsRepository;
    private readonly TokenManagementService _tokenManagement;
    private readonly CodexTuiService _codexTuiService;
    private readonly AuthJsonService _authJsonService;

    public StatsCommand(
        IIdentityRepository identityRepository,
        IUsageStatsRepository usageStatsRepository,
        TokenManagementService tokenManagement,
        CodexTuiService codexTuiService,
        AuthJsonService authJsonService)
    {
        _identityRepository = identityRepository;
        _usageStatsRepository = usageStatsRepository;
        _tokenManagement = tokenManagement;
        _codexTuiService = codexTuiService;
        _authJsonService = authJsonService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, StatsSettings settings)
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

        // Get current token
        var authToken = await _tokenManagement.GetCurrentTokenAsync(identity.Id);
        if (authToken == null)
        {
            AnsiConsole.MarkupLine($"[red]No token found for identity:[/] {identity.Email}");
            return 1;
        }

        Core.Models.UsageStats? stats = null;

        try
        {
            await AnsiConsole.Status()
                .StartAsync($"Retrieving usage stats for [bold]{identity.Email}[/]...", async ctx =>
                {
                    ctx.Status("Launching Codex TUI...");

                    // Get stats from Codex TUI
                    stats = await _codexTuiService.GetUsageStatsAsync(identity.Id, authToken);

                    ctx.Status("Saving stats to database...");

                    // Save to database
                    await _usageStatsRepository.CreateAsync(stats);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]⚠[/] Automatic stats retrieval failed:");
            AnsiConsole.MarkupLine($"[dim]{ex.Message}[/]");
            AnsiConsole.WriteLine();

            var panel = new Panel(new Markup(@"[bold cyan]Alternative: Use manual entry[/]

Run the following command to enter stats manually:

    [yellow]codex-tokens stats-entry[/]

Or specify an identity:

    [yellow]codex-tokens stats-entry {identity.Email}[/]

This will guide you through entering the values from [yellow]codex --yolo[/]."))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };

            AnsiConsole.Write(panel);
            return 1;
        }

        if (stats == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to retrieve stats[/]");
            return 1;
        }

        // Display stats in a nice table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Metric[/]");
        table.AddColumn("[bold]Usage[/]");
        table.AddColumn("[bold]Resets[/]");

        // 5-hour limit row
        var fiveHourBar = CreateProgressBar(stats.FiveHourLimitPercent);
        var fiveHourReset = stats.FiveHourLimitResetTime.ToString("HH:mm");
        table.AddRow(
            "5h limit",
            $"{fiveHourBar} {stats.FiveHourLimitPercent}% used",
            $"{fiveHourReset}"
        );

        // Weekly limit row
        var weeklyBar = CreateProgressBar(stats.WeeklyLimitPercent);
        var weeklyReset = stats.WeeklyLimitResetTime.ToString("HH:mm 'on' dd MMM");
        table.AddRow(
            "Weekly limit",
            $"{weeklyBar} {stats.WeeklyLimitPercent}% used",
            $"{weeklyReset}"
        );

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Usage statistics for {identity.Email}[/]");
        AnsiConsole.Write(table);

        return 0;
    }

    private string CreateProgressBar(int percent)
    {
        const int barLength = 20;
        var filled = (int)Math.Round(percent / 100.0 * barLength);
        var empty = barLength - filled;

        var color = percent switch
        {
            >= 80 => "red",
            >= 50 => "yellow",
            _ => "green"
        };

        return $"[{color}]{'█'.ToString().PadRight(filled, '█')}[/]{'░'.ToString().PadRight(empty, '░')}";
    }
}
