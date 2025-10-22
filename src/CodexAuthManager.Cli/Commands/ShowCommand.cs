using CodexAuthManager.Core.Data;

namespace CodexAuthManager.Cli.Commands;

/// <summary>
/// Handles the show command - displays identity details
/// </summary>
public class ShowCommand
{
    private readonly IIdentityRepository _identityRepository;
    private readonly ITokenVersionRepository _tokenVersionRepository;

    public ShowCommand(
        IIdentityRepository identityRepository,
        ITokenVersionRepository tokenVersionRepository)
    {
        _identityRepository = identityRepository;
        _tokenVersionRepository = tokenVersionRepository;
    }

    public async Task<int> ExecuteAsync(string? identifier = null)
    {
        // Get identity - either by ID, email, or active one
        Core.Models.Identity? identity = null;

        if (string.IsNullOrEmpty(identifier))
        {
            identity = await _identityRepository.GetActiveIdentityAsync();
            if (identity == null)
            {
                Console.WriteLine("No active identity found. Please specify an ID or email.");
                return 1;
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
            Console.WriteLine($"Identity not found: {identifier}");
            return 1;
        }

        // Display identity details
        Console.WriteLine($"Identity #{identity.Id}");
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"Email:          {identity.Email}");
        Console.WriteLine($"User ID:        {identity.UserId}");
        Console.WriteLine($"Account ID:     {identity.AccountId}");
        Console.WriteLine($"Plan Type:      {identity.PlanType}");
        Console.WriteLine($"Active:         {(identity.IsActive ? "Yes" : "No")}");
        Console.WriteLine($"Created:        {identity.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Last Updated:   {identity.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

        // Display token versions
        var versions = (await _tokenVersionRepository.GetVersionsAsync(identity.Id)).ToList();
        Console.WriteLine($"\nToken Versions ({versions.Count}):");
        Console.WriteLine(new string('-', 80));

        foreach (var version in versions)
        {
            var current = version.IsCurrent ? " [CURRENT]" : "";
            var lastRefresh = version.LastRefresh.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Console.WriteLine($"  v{version.VersionNumber}: Created {version.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}, " +
                            $"Last refresh: {lastRefresh}{current}");
        }

        return 0;
    }
}
