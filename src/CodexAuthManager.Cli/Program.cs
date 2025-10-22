using Microsoft.Extensions.DependencyInjection;
using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Data;
using CodexAuthManager.Core.Infrastructure;
using CodexAuthManager.Core.Services;
using CodexAuthManager.Cli.Commands;

namespace CodexAuthManager.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Determine environment (check for --dev flag or CODEX_ENV environment variable)
        bool isDevelopment = args.Contains("--dev") ||
                            Environment.GetEnvironmentVariable("CODEX_ENV") == "development";

        // Remove --dev from args if present
        args = args.Where(a => a != "--dev").ToArray();

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services, isDevelopment);
        var serviceProvider = services.BuildServiceProvider();

        // Initialize database
        var database = serviceProvider.GetRequiredService<TokenDatabase>();
        await database.InitializeAsync();

        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLower();
        var commandArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "import" => await ExecuteImportAsync(serviceProvider),
                "list" => await ExecuteListAsync(serviceProvider),
                "show" => await ExecuteShowAsync(serviceProvider, commandArgs),
                "activate" => await ExecuteActivateAsync(serviceProvider, commandArgs),
                "remove" => await ExecuteRemoveAsync(serviceProvider, commandArgs),
                "rollback" => await ExecuteRollbackAsync(serviceProvider, commandArgs),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => ShowError($"Unknown command: {command}")
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ExecuteImportAsync(ServiceProvider serviceProvider)
    {
        var cmd = serviceProvider.GetRequiredService<ImportCommand>();
        return await cmd.ExecuteAsync();
    }

    private static async Task<int> ExecuteListAsync(ServiceProvider serviceProvider)
    {
        var cmd = serviceProvider.GetRequiredService<ListCommand>();
        return await cmd.ExecuteAsync();
    }

    private static async Task<int> ExecuteShowAsync(ServiceProvider serviceProvider, string[] args)
    {
        var cmd = serviceProvider.GetRequiredService<ShowCommand>();
        var identifier = args.Length > 0 ? args[0] : null;
        return await cmd.ExecuteAsync(identifier);
    }

    private static async Task<int> ExecuteActivateAsync(ServiceProvider serviceProvider, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Error: Please specify an identity ID or email to activate");
            return 1;
        }

        var cmd = serviceProvider.GetRequiredService<ActivateCommand>();
        return await cmd.ExecuteAsync(args[0]);
    }

    private static async Task<int> ExecuteRemoveAsync(ServiceProvider serviceProvider, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Error: Please specify at least one identity ID or email to remove");
            return 1;
        }

        var cmd = serviceProvider.GetRequiredService<RemoveCommand>();
        return await cmd.ExecuteAsync(args);
    }

    private static async Task<int> ExecuteRollbackAsync(ServiceProvider serviceProvider, string[] args)
    {
        var identifier = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
        int? version = null;

        // Look for --version flag
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--version" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var v))
                {
                    version = v;
                }
                break;
            }
        }

        var cmd = serviceProvider.GetRequiredService<RollbackCommand>();
        return await cmd.ExecuteAsync(identifier, version);
    }

    private static int ShowHelp()
    {
        Console.WriteLine("Codex Token Manager - Manage multiple Codex authentication tokens");
        Console.WriteLine();
        Console.WriteLine("Usage: codex-tokens <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  import                      Scan and import *auth.json files from the Codex folder");
        Console.WriteLine("  list                        List all stored identities/tokens");
        Console.WriteLine("  show [identifier]           Show identity details (current one by default)");
        Console.WriteLine("  activate <identifier>       Mark an identity as active and update auth.json");
        Console.WriteLine("  remove <identifiers...>     Delete one or more identities");
        Console.WriteLine("  rollback [identifier]       Restore a previous version of an identity");
        Console.WriteLine("                              [--version N]  Specify version number to rollback to");
        Console.WriteLine("  help                        Show this help message");
        Console.WriteLine();
        Console.WriteLine("Global options:");
        Console.WriteLine("  --dev                       Use development environment");
        Console.WriteLine();
        Console.WriteLine("Environment:");
        Console.WriteLine("  Set CODEX_ENV=development to use development paths by default");
        return 0;
    }

    private static int ShowError(string message)
    {
        Console.WriteLine(message);
        Console.WriteLine("Use 'codex-tokens help' for usage information");
        return 1;
    }

    private static void ConfigureServices(ServiceCollection services, bool isDevelopment)
    {
        // Infrastructure
        services.AddSingleton<IFileSystem, RealFileSystem>();

        if (isDevelopment)
        {
            services.AddSingleton<IPathProvider, DevelopmentPathProvider>();
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

        // Services
        services.AddSingleton<JwtDecoderService>();
        services.AddSingleton<TokenManagementService>();
        services.AddSingleton<AuthJsonService>();
        services.AddSingleton<DatabaseBackupService>();

        // Commands
        services.AddSingleton<ImportCommand>();
        services.AddSingleton<ListCommand>();
        services.AddSingleton<ShowCommand>();
        services.AddSingleton<ActivateCommand>();
        services.AddSingleton<RemoveCommand>();
        services.AddSingleton<RollbackCommand>();
    }
}
