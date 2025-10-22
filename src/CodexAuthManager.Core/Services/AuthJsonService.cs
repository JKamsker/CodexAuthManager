using System.Text.Json;
using System.Text.Json.Serialization;
using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Services;

/// <summary>
/// Service for reading and writing auth.json files
/// </summary>
public class AuthJsonService
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathProvider _pathProvider;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuthJsonService(IFileSystem fileSystem, IPathProvider pathProvider)
    {
        _fileSystem = fileSystem;
        _pathProvider = pathProvider;
    }

    /// <summary>
    /// Scans the Codex folder for all auth.json files
    /// </summary>
    public IEnumerable<string> ScanForAuthFiles()
    {
        var codexFolder = _pathProvider.GetCodexFolderPath();
        return _fileSystem.EnumerateFiles(codexFolder, "*auth.json");
    }

    /// <summary>
    /// Reads an auth.json file and deserializes it
    /// </summary>
    public AuthToken ReadAuthToken(string filePath)
    {
        var content = _fileSystem.ReadAllText(filePath);
        var token = JsonSerializer.Deserialize<AuthToken>(content, _jsonOptions);
        return token ?? throw new InvalidOperationException($"Failed to deserialize auth token from {filePath}");
    }

    /// <summary>
    /// Writes an auth.json file
    /// </summary>
    public void WriteAuthToken(string filePath, AuthToken authToken)
    {
        var content = JsonSerializer.Serialize(authToken, _jsonOptions);
        _fileSystem.WriteAllText(filePath, content);
    }

    /// <summary>
    /// Writes the active auth.json file
    /// </summary>
    public void WriteActiveAuthToken(AuthToken authToken)
    {
        var activeAuthPath = _pathProvider.GetActiveAuthJsonPath();
        WriteAuthToken(activeAuthPath, authToken);
    }

    /// <summary>
    /// Reads the active auth.json file
    /// </summary>
    public AuthToken? ReadActiveAuthToken()
    {
        var activeAuthPath = _pathProvider.GetActiveAuthJsonPath();
        if (!_fileSystem.FileExists(activeAuthPath))
            return null;

        return ReadAuthToken(activeAuthPath);
    }
}
