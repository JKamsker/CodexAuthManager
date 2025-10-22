using CodexAuthManager.Core.Data;

namespace CodexAuthManager.Cli.Commands;

/// <summary>
/// Handles the list command - displays all identities
/// </summary>
public class ListCommand
{
    private readonly IIdentityRepository _identityRepository;

    public ListCommand(IIdentityRepository identityRepository)
    {
        _identityRepository = identityRepository;
    }

    public async Task<int> ExecuteAsync()
    {
        var identities = (await _identityRepository.GetAllAsync()).ToList();

        if (!identities.Any())
        {
            Console.WriteLine("No identities found.");
            return 0;
        }

        Console.WriteLine($"Found {identities.Count} identity/identities:\n");
        Console.WriteLine($"{"ID",-5} {"Email",-35} {"Plan",-10} {"Active",-8} {"Last Updated"}");
        Console.WriteLine(new string('-', 85));

        foreach (var identity in identities)
        {
            var active = identity.IsActive ? "✓" : "";
            var updated = identity.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Console.WriteLine($"{identity.Id,-5} {identity.Email,-35} {identity.PlanType,-10} {active,-8} {updated}");
        }

        return 0;
    }
}
