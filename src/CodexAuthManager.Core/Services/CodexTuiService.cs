using System.Text.Json;
using CodexAuthManager.Core.Abstractions;
using CodexAuthManager.Core.Models;

namespace CodexAuthManager.Core.Services;

/// <summary>
/// Service for interacting with Codex to retrieve usage statistics from session history files
/// </summary>
public class CodexTuiService
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathProvider _pathProvider;
    private readonly AuthJsonService _authJsonService;
    private readonly ICodexProcessRunner _processRunner;

    public CodexTuiService(
        IFileSystem fileSystem,
        IPathProvider pathProvider,
        AuthJsonService authJsonService,
        ICodexProcessRunner processRunner)
    {
        _fileSystem = fileSystem;
        _pathProvider = pathProvider;
        _authJsonService = authJsonService;
        _processRunner = processRunner;
    }

    /// <summary>
    /// Retrieves usage stats by launching Codex and parsing the session history file
    /// </summary>
    public async Task<UsageStats> GetUsageStatsAsync(int identityId, AuthToken authToken)
    {
        // Write the auth token to the active auth.json temporarily
        var originalAuthJson = _authJsonService.ReadActiveAuthToken();
        try
        {
            _authJsonService.WriteActiveAuthToken(authToken);

            // Launch codex exec to generate a session file
            var sessionContent = await _processRunner.RunCodexWithStatusAsync();

            // Parse the session file content
            return ParseUsageStats(sessionContent, identityId);
        }
        finally
        {
            // Restore original auth.json
            if (originalAuthJson != null)
            {
                _authJsonService.WriteActiveAuthToken(originalAuthJson);
            }
        }
    }

    private UsageStats ParseUsageStats(string sessionContent, int identityId)
    {
        // Parse JSONL format (each line is a separate JSON object)
        var lines = sessionContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // Look for event_msg with token_count type
                if (root.TryGetProperty("type", out var typeElement) &&
                    typeElement.GetString() == "event_msg")
                {
                    if (root.TryGetProperty("payload", out var payload) &&
                        payload.TryGetProperty("type", out var payloadType) &&
                        payloadType.GetString() == "token_count")
                    {
                        if (payload.TryGetProperty("rate_limits", out var rateLimits))
                        {
                            return ParseRateLimits(rateLimits, identityId);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Skip invalid JSON lines
                continue;
            }
        }

        // Save output to temp file for debugging
        var debugFile = Path.Combine(Path.GetTempPath(), $"codex-parse-debug-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
        File.WriteAllText(debugFile, sessionContent);
        throw new InvalidOperationException($"Failed to parse usage stats from Codex session file. Debug output saved to: {debugFile}");
    }

    private UsageStats ParseRateLimits(JsonElement rateLimits, int identityId)
    {
        var now = DateTime.Now;

        DateTime ParseResetTime(JsonElement scope)
        {
            // New format: resets_at is a Unix timestamp (seconds) when the window resets
            if (scope.TryGetProperty("resets_at", out var resetsAtEl))
            {
                var epoch = resetsAtEl.ValueKind switch
                {
                    JsonValueKind.Number => resetsAtEl.GetInt64(),
                    JsonValueKind.String when long.TryParse(resetsAtEl.GetString(), out var v) => v,
                    _ => (long?)null
                };
                if (epoch is long ts)
                {
                    // Convert to local time to keep behavior consistent with previous implementation
                    return DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().DateTime;
                }
            }

            // Backward compatibility: previous format used resets_in_seconds
            if (scope.TryGetProperty("resets_in_seconds", out var resetsInEl) && resetsInEl.ValueKind == JsonValueKind.Number)
            {
                return now.AddSeconds(resetsInEl.GetDouble());
            }

            // As a last resort, if window_minutes is provided but no reset indicator, estimate reset as now + window
            if (scope.TryGetProperty("window_minutes", out var windowMinEl) && windowMinEl.ValueKind == JsonValueKind.Number)
            {
                return now.AddMinutes(windowMinEl.GetDouble());
            }

            throw new InvalidOperationException("Unable to determine rate limit reset time from rate_limits scope.");
        }

        // Parse primary (5-hour limit)
        var primary = rateLimits.GetProperty("primary");
        var fiveHourPercent = (int)Math.Round(primary.GetProperty("used_percent").GetDouble());
        var fiveHourResetTime = ParseResetTime(primary);

        // Parse secondary (weekly limit)
        var secondary = rateLimits.GetProperty("secondary");
        var weeklyPercent = (int)Math.Round(secondary.GetProperty("used_percent").GetDouble());
        var weeklyResetTime = ParseResetTime(secondary);

        return new UsageStats
        {
            IdentityId = identityId,
            FiveHourLimitPercent = fiveHourPercent,
            FiveHourLimitResetTime = fiveHourResetTime,
            WeeklyLimitPercent = weeklyPercent,
            WeeklyLimitResetTime = weeklyResetTime,
            CapturedAt = DateTime.UtcNow
        };
    }
}
