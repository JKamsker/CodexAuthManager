using CodexAuthManager.Core.Abstractions;

namespace CodexAuthManager.Tests.TestHelpers;

/// <summary>
/// Mock implementation of ICodexProcessRunner for testing
/// </summary>
public class MockCodexProcessRunner : ICodexProcessRunner
{
    private readonly string _outputToReturn;

    public MockCodexProcessRunner(string outputToReturn)
    {
        _outputToReturn = outputToReturn;
    }

    public MockCodexProcessRunner()
    {
        // Default output with sample JSONL session data
        // Simulating 0% usage with resets in 10185 seconds (2.8 hours) and 596985 seconds (6.9 days)
        _outputToReturn = CreateSampleJsonl(0.0, 10185, 0.0, 596985);
    }

    public Task<string> RunCodexWithStatusAsync()
    {
        return Task.FromResult(_outputToReturn);
    }

    public static string CreateSampleJsonl(double fiveHourPercent, int fiveHourResetsInSeconds,
        double weeklyPercent, int weeklyResetsInSeconds)
    {
        return $@"{{""timestamp"":""2025-10-22T13:31:50.905Z"",""type"":""session_meta"",""payload"":{{""id"":""test-session-id"",""timestamp"":""2025-10-22T13:31:50.871Z""}}}}
{{""timestamp"":""2025-10-22T13:31:55.363Z"",""type"":""event_msg"",""payload"":{{""type"":""token_count"",""info"":null,""rate_limits"":{{""primary"":{{""used_percent"":{fiveHourPercent},""window_minutes"":299,""resets_in_seconds"":{fiveHourResetsInSeconds}}},""secondary"":{{""used_percent"":{weeklyPercent},""window_minutes"":10079,""resets_in_seconds"":{weeklyResetsInSeconds}}}}}}}}}
{{""timestamp"":""2025-10-22T13:31:56.584Z"",""type"":""event_msg"",""payload"":{{""type"":""agent_message"",""message"":""Hey! What can I help you with today?""}}}}
";
    }
}
