using Xunit;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// Defines a test collection to prevent command tests from running in parallel.
/// This is necessary because Spectre.Console interactive displays cannot run concurrently.
/// </summary>
[CollectionDefinition("CommandTests", DisableParallelization = true)]
public class CommandTestCollection
{
}
