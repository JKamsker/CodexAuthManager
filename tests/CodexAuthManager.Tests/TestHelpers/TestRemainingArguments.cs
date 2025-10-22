using Spectre.Console.Cli;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// Test implementation of IRemainingArguments for unit testing
/// </summary>
public class TestRemainingArguments : IRemainingArguments
{
    public IReadOnlyList<string> Raw { get; }
    public ILookup<string, string?> Parsed { get; }

    public TestRemainingArguments()
    {
        Raw = Array.Empty<string>();
        Parsed = Enumerable.Empty<KeyValuePair<string, string?>>()
            .ToLookup(x => x.Key, x => x.Value);
    }
}
