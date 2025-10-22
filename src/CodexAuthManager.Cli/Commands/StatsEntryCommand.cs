using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Models;
using CodexAuthManager.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace CodexAuthManager.Cli.Commands;

public class StatsEntrySettings : CommandSettings
{
    [Description("Identity ID, email, or leave empty for active identity")]
    [CommandArgument(0, "[identifier]")]
    public string? Identifier { get; set; }
}

/// <summary>
/// Handles manual stats entry - prompts user to enter usage statistics
/// </summary>
public class StatsEntryCommand : AsyncCommand<StatsEntrySettings>
{
    private readonly IIdentityRepository _identityRepository;
    private readonly IUsageStatsRepository _usageStatsRepository;
    private readonly AuthJsonService _authJsonService;

    public StatsEntryCommand(
        IIdentityRepository identityRepository,
        IUsageStatsRepository usageStatsRepository,
        AuthJsonService authJsonService)
    {
        _identityRepository = identityRepository;
        _usageStatsRepository = usageStatsRepository;
        _authJsonService = authJsonService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, StatsEntrySettings settings)
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

        // Show instructions
        var panel = new Panel(new Markup(@"[bold cyan]How to get your usage stats:[/]

1. Open a new terminal window
2. Run: [yellow]codex --yolo[/]
3. Type: [yellow]hi[/] (and press Enter)
4. Type: [yellow]/status[/] (and press Enter)
5. Note the values shown for:
   • 5h limit: [green]X%[/] used (resets [green]HH:MM[/])
   • Weekly limit: [green]X%[/] used (resets [green]HH:MM[/] on [green]DD MMM[/])

Then enter the values below."))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Entering stats for:[/] {identity.Email}");
        AnsiConsole.WriteLine();

        // Prompt for 5h limit percentage
        var fiveHourPercent = AnsiConsole.Prompt(
            new TextPrompt<int>("[green]5h limit percentage (0-100):[/]")
                .ValidationErrorMessage("[red]Please enter a number between 0 and 100[/]")
                .Validate(p => p >= 0 && p <= 100));

        // Prompt for 5h limit reset time
        var fiveHourReset = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]5h limit resets at (HH:MM, e.g. 18:21):[/]")
                .ValidationErrorMessage("[red]Please enter time in HH:MM format[/]")
                .Validate(t =>
                {
                    return TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
                }));

        // Prompt for weekly limit percentage
        var weeklyPercent = AnsiConsole.Prompt(
            new TextPrompt<int>("[green]Weekly limit percentage (0-100):[/]")
                .ValidationErrorMessage("[red]Please enter a number between 0 and 100[/]")
                .Validate(p => p >= 0 && p <= 100));

        // Prompt for weekly limit reset time
        var weeklyReset = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Weekly limit resets at (HH:MM, e.g. 12:21):[/]")
                .ValidationErrorMessage("[red]Please enter time in HH:MM format[/]")
                .Validate(t =>
                {
                    return TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
                }));

        // Prompt for weekly limit reset date
        var weeklyDate = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Weekly limit resets on (DD MMM, e.g. 29 Oct):[/]")
                .ValidationErrorMessage("[red]Please enter date in DD MMM format[/]")
                .Validate(d =>
                {
                    return DateTime.TryParseExact(d + " " + DateTime.Now.Year, "dd MMM yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
                }));

        // Parse the inputs
        var fiveHourTime = TimeOnly.ParseExact(fiveHourReset, "HH:mm", CultureInfo.InvariantCulture);
        var weeklyTime = TimeOnly.ParseExact(weeklyReset, "HH:mm", CultureInfo.InvariantCulture);

        var now = DateTime.Now;
        var fiveHourResetTime = new DateTime(now.Year, now.Month, now.Day, fiveHourTime.Hour, fiveHourTime.Minute, 0);

        // If the reset time is in the past, it's tomorrow
        if (fiveHourResetTime < now)
        {
            fiveHourResetTime = fiveHourResetTime.AddDays(1);
        }

        var weeklyResetDate = DateTime.ParseExact(weeklyDate + " " + DateTime.Now.Year, "dd MMM yyyy",
            CultureInfo.InvariantCulture);

        // If the date is in the past, it's next year
        if (weeklyResetDate < now.Date)
        {
            weeklyResetDate = weeklyResetDate.AddYears(1);
        }

        var weeklyResetTime = new DateTime(weeklyResetDate.Year, weeklyResetDate.Month, weeklyResetDate.Day,
            weeklyTime.Hour, weeklyTime.Minute, 0);

        // Create and save stats
        var stats = new UsageStats
        {
            IdentityId = identity.Id,
            FiveHourLimitPercent = fiveHourPercent,
            FiveHourLimitResetTime = fiveHourResetTime,
            WeeklyLimitPercent = weeklyPercent,
            WeeklyLimitResetTime = weeklyResetTime,
            CapturedAt = DateTime.UtcNow
        };

        await _usageStatsRepository.CreateAsync(stats);

        // Display confirmation
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Metric[/]");
        table.AddColumn("[bold]Usage[/]");
        table.AddColumn("[bold]Resets[/]");

        var fiveHourBar = CreateProgressBar(fiveHourPercent);
        table.AddRow(
            "5h limit",
            $"{fiveHourBar} {fiveHourPercent}% used",
            $"{fiveHourResetTime:HH:mm}"
        );

        var weeklyBar = CreateProgressBar(weeklyPercent);
        table.AddRow(
            "Weekly limit",
            $"{weeklyBar} {weeklyPercent}% used",
            $"{weeklyResetTime:HH:mm 'on' dd MMM}"
        );

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓[/] Stats saved successfully!");
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
