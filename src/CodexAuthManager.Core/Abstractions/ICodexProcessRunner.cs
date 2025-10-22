namespace CodexAuthManager.Core.Abstractions;

/// <summary>
/// Interface for running the Codex process
/// </summary>
public interface ICodexProcessRunner
{
    Task<string> RunCodexWithStatusAsync();
}
