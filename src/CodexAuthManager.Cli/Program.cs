using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Infrastructure;
using CodexAuthManager.Core.Services;
using CodexAuthManager.Cli.Commands;
using CodexAuthManager.Cli.Infrastructure;

namespace CodexAuthManager.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Determine environment (check for --dev flag or CODEX_ENV environment variable)
        bool isDevelopment = args.Contains("--dev") ||
                            Environment.GetEnvironmentVariable("CODEX_ENV") == "development";

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services, isDevelopment);

        // Create Spectre type registrar
        var registrar = new TypeRegistrar(services);

        // Create command app
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("codex-tokens");

            config.AddCommand<ImportCommand>("import")
                .WithDescription("Scan and import *auth.json files from the Codex folder")
                .WithExample(new[] { "import" });

            config.AddCommand<ListCommand>("list")
                .WithDescription("List all stored identities/tokens")
                .WithExample(new[] { "list" });

            config.AddCommand<ShowCommand>("show")
                .WithDescription("Show identity details (current one by default)")
                .WithExample(new[] { "show" })
                .WithExample(new[] { "show", "2" })
                .WithExample(new[] { "show", "user@example.com" });

            config.AddCommand<ActivateCommand>("activate")
                .WithDescription("Mark an identity as active and update auth.json")
                .WithExample(new[] { "activate", "2" })
                .WithExample(new[] { "activate", "user@example.com" });

            config.AddCommand<RemoveCommand>("remove")
                .WithDescription("Delete one or more identities")
                .WithExample(new[] { "remove", "2" })
                .WithExample(new[] { "remove", "user@example.com" })
                .WithExample(new[] { "remove", "1", "2", "3" });

            config.AddCommand<RollbackCommand>("rollback")
                .WithDescription("Restore a previous version of an identity")
                .WithExample(new[] { "rollback" })
                .WithExample(new[] { "rollback", "2" })
                .WithExample(new[] { "rollback", "user@example.com", "--version", "3" });

            config.AddCommand<StatsCommand>("stats")
                .WithDescription("Retrieve and save current usage statistics (5h limit, weekly limit)")
                .WithExample(new[] { "stats" })
                .WithExample(new[] { "stats", "2" })
                .WithExample(new[] { "stats", "user@example.com" });

            config.AddCommand<StatsEntryCommand>("stats-entry")
                .WithDescription("Manually enter usage statistics (interactive)")
                .WithExample(new[] { "stats-entry" })
                .WithExample(new[] { "stats-entry", "2" })
                .WithExample(new[] { "stats-entry", "user@example.com" });

            config.ValidateExamples();
        });

        // Initialize database before running commands
        ServiceProvider? bootstrapProvider = null;
        try
        {
            bootstrapProvider = services.BuildServiceProvider();
            var database = bootstrapProvider.GetRequiredService<TokenDatabase>();
            await database.InitializeAsync();

            var maintenance = bootstrapProvider.GetRequiredService<TokenMaintenanceService>();
            await maintenance.DeduplicateAndFixCurrentVersionsAsync();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to initialize database:[/] {ex.Message}");
            return 1;
        }
        finally
        {
            bootstrapProvider?.Dispose();
        }

        return await app.RunAsync(args);
    }

    private static void ConfigureServices(ServiceCollection services, bool isDevelopment)
    {
        // Infrastructure
        services.AddSingleton<IFileSystem, RealFileSystem>();

        if (isDevelopment)
        {
            services.AddSingleton<IPathProvider, DevelopmentPathProvider>();
            AnsiConsole.MarkupLine("[yellow]Running in development mode[/]");
        }
        else
        {
            services.AddSingleton<IPathProvider, ProductionPathProvider>();
        }

        // Database
        services.AddSingleton(sp =>
        {
            var pathProvider = sp.GetRequiredService<IPathProvider>();
            return new TokenDatabase(pathProvider, useInMemory: false);
        });

        // Repositories
        services.AddSingleton<IIdentityRepository>(sp =>
        {
            var database = sp.GetRequiredService<TokenDatabase>();
            return new IdentityRepository(database);
        });

        services.AddSingleton<ITokenVersionRepository>(sp =>
        {
            var database = sp.GetRequiredService<TokenDatabase>();
            return new TokenVersionRepository(database);
        });

        services.AddSingleton<IUsageStatsRepository>(sp =>
        {
            var database = sp.GetRequiredService<TokenDatabase>();
            return new UsageStatsRepository(database);
        });

        // Services
        services.AddSingleton<JwtDecoderService>();
        services.AddSingleton<TokenManagementService>();
        services.AddSingleton<AuthJsonService>();
        services.AddSingleton<TokenMaintenanceService>();
        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton<ICodexProcessRunner, CodexProcessRunner>();
        services.AddSingleton<CodexTuiService>();

        // Commands
        services.AddSingleton<ImportCommand>();
        services.AddSingleton<ListCommand>();
        services.AddSingleton<ShowCommand>();
        services.AddSingleton<ActivateCommand>();
        services.AddSingleton<RemoveCommand>();
        services.AddSingleton<RollbackCommand>();
        services.AddSingleton<StatsCommand>();
        services.AddSingleton<StatsEntryCommand>();
    }
}
