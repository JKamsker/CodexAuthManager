using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodexAuthManager.Cli.Commands;

public class StatsSettings : CommandSettings
{
    [Description("Identity ID, email, 'all' for all users, or leave empty for active identity")]
    [CommandArgument(0, "[identifier]")]
    public string? Identifier { get; set; }

    [Description("Refresh stats from Codex before displaying")]
    [CommandOption("-r|--refresh")]
    public bool Refresh { get; set; }
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
        // Handle "all" identifier
        if (settings.Identifier?.Equals("all", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await ShowAllStatsAsync(settings.Refresh);
        }

        // Get single identity
        var identity = await GetIdentityAsync(settings.Identifier);
        if (identity == null)
        {
            return 1;
        }

        // Refresh stats if requested
        if (settings.Refresh)
        {
            var success = await RefreshStatsAsync(identity);
            if (!success)
            {
                return 1;
            }
        }

        // Display stats from database
        var stats = await _usageStatsRepository.GetLatestAsync(identity.Id);
        if (stats == null)
        {
            AnsiConsole.MarkupLine($"[yellow]No stats found for {identity.Email}.[/]");
            AnsiConsole.MarkupLine($"[dim]Run [/][yellow]stats --refresh[/][dim] to retrieve stats from Codex.[/]");
            return 1;
        }

        DisplayStats(identity, stats);
        return 0;
    }

    private async Task<Core.Models.Identity?> GetIdentityAsync(string? identifier)
    {
        Core.Models.Identity? identity = null;

        if (string.IsNullOrEmpty(identifier))
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
                return null;
            }
        }
        else if (int.TryParse(identifier, out int id))
        {
            identity = await _identityRepository.GetByIdAsync(id);
        }
        else
        {
            identity = await _identityRepository.GetByEmailAsync(identifier);
        }

        if (identity == null)
        {
            AnsiConsole.MarkupLine($"[red]Identity not found:[/] {identifier}");
            return null;
        }

        return identity;
    }

    private async Task<bool> RefreshStatsAsync(Core.Models.Identity identity)
    {
        // Get current token
        var authToken = await _tokenManagement.GetCurrentTokenAsync(identity.Id);
        if (authToken == null)
        {
            AnsiConsole.MarkupLine($"[red]No token found for identity:[/] {identity.Email}");
            return false;
        }

        CodexStatsResult? statsResult = null;

        try
        {
            await AnsiConsole.Status()
                .StartAsync($"Refreshing stats for [bold]{identity.Email}[/]...", async ctx =>
                {
                    ctx.Status("Launching Codex...");

                    // Get stats from Codex
                    statsResult = await _codexTuiService.GetUsageStatsAsync(identity.Id, authToken);

                    ctx.Status("Saving to database...");

                    // Save to database
                    await _usageStatsRepository.CreateAsync(statsResult.UsageStats);

                    ctx.Status("Importing refreshed auth.json...");

                    // Codex may refresh tokens while fetching stats, so keep the database in sync
                    var latestAuthToken = statsResult.UpdatedAuthToken ?? authToken;
                    await _tokenManagement.ImportOrUpdateTokenAsync(latestAuthToken);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Stats refreshed for {identity.Email}");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]⚠[/] Failed to refresh stats for {identity.Email}:");
            AnsiConsole.MarkupLine($"[dim]{ex.Message}[/]");
            AnsiConsole.WriteLine();

            var panel = new Panel(new Markup(@"[bold cyan]Alternative: Use manual entry[/]

Run the following command to enter stats manually:

    [yellow]codex-tokens stats-entry[/]

Or specify an identity:

    [yellow]codex-tokens stats-entry " + identity.Email + @"[/]

This will guide you through entering the values from [yellow]codex --yolo[/]."))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };

            AnsiConsole.Write(panel);
            return false;
        }
    }

    private async Task<int> ShowAllStatsAsync(bool refresh)
    {
        var identities = (await _identityRepository.GetAllAsync()).ToList();

        if (!identities.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No identities found.[/]");
            return 0;
        }

        // Refresh all if requested
        if (refresh)
        {
            AnsiConsole.MarkupLine($"[cyan]Refreshing stats for {identities.Count} identities...[/]");
            AnsiConsole.WriteLine();

            foreach (var identity in identities)
            {
                await RefreshStatsAsync(identity);
            }

            AnsiConsole.WriteLine();
        }

        // Create consolidated table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]ID[/]").RightAligned());
        table.AddColumn("[bold]Email[/]");
        table.AddColumn("[bold]Plan[/]");
        table.AddColumn(new TableColumn("[bold]5h Limit[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]7d Limit[/]").RightAligned());
        table.AddColumn("[bold]5h Reset[/]");
        table.AddColumn("[bold]7d Reset[/]");
        table.AddColumn("[bold]Last Updated[/]");

        // Add rows for each identity
        foreach (var identity in identities)
        {
            var stats = await _usageStatsRepository.GetLatestAsync(identity.Id);

            if (stats == null)
            {
                table.AddRow(
                    identity.Id.ToString(),
                    identity.Email,
                    identity.PlanType,
                    "[dim]N/A[/]",
                    "[dim]N/A[/]",
                    "[dim]N/A[/]",
                    "[dim]N/A[/]",
                    "[dim]Never[/]"
                );
            }
            else
            {
                var fiveHourColor = GetPercentColor(stats.FiveHourLimitPercent);
                var weeklyColor = GetPercentColor(stats.WeeklyLimitPercent);
                var fiveHourReset = stats.FiveHourLimitResetTime.ToString("HH:mm");
                var weeklyReset = stats.WeeklyLimitResetTime.ToString("HH:mm dd MMM");
                var lastUpdated = stats.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                table.AddRow(
                    identity.Id.ToString(),
                    identity.Email,
                    identity.PlanType,
                    $"[{fiveHourColor}]{stats.FiveHourLimitPercent}%[/]",
                    $"[{weeklyColor}]{stats.WeeklyLimitPercent}%[/]",
                    fiveHourReset,
                    weeklyReset,
                    $"[dim]{lastUpdated}[/]"
                );
            }
        }

        AnsiConsole.MarkupLine("[bold cyan]Usage statistics for all identities:[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        return 0;
    }

    private string GetPercentColor(int percent)
    {
        return percent switch
        {
            >= 80 => "red",
            >= 50 => "yellow",
            _ => "green"
        };
    }

    private void DisplayStats(Core.Models.Identity identity, Core.Models.UsageStats stats)
    {
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

        AnsiConsole.MarkupLine($"[bold]Usage statistics for {identity.Email}[/]");
        AnsiConsole.Write(table);
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
