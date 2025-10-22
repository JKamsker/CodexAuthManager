using CodexAuthManager.Core.Services;
using CodexAuthManager.Tests.TestHelpers;
using Xunit;

namespace CodexAuthManager.Tests.Services;

public class CodexTuiServiceTests : IDisposable
{
    private readonly TestFixture _fixture;

    public CodexTuiServiceTests()
    {
        _fixture = new TestFixture();
    }

    [Fact]
    public void ParseUsageStats_ShouldParseValidJsonl()
    {
        // This test verifies that the JSONL parsing works correctly
        var sampleJsonl = MockCodexProcessRunner.CreateSampleJsonl(
            fiveHourPercent: 0.0,
            fiveHourResetsInSeconds: 3600, // 1 hour
            weeklyPercent: 0.0,
            weeklyResetsInSeconds: 86400 // 1 day
        );

        // Use reflection to call the private ParseUsageStats method
        var method = typeof(CodexTuiService).GetMethod("ParseUsageStats",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var beforeParse = DateTime.Now;
        var result = method!.Invoke(_fixture.CodexTuiService, new object[] { sampleJsonl, 1 }) as CodexAuthManager.Core.Models.UsageStats;
        var afterParse = DateTime.Now;

        Assert.NotNull(result);
        Assert.Equal(1, result.IdentityId);
        Assert.Equal(0, result.FiveHourLimitPercent);
        Assert.Equal(0, result.WeeklyLimitPercent);

        // Verify reset times are calculated correctly (within tolerance)
        var expectedFiveHourReset = beforeParse.AddSeconds(3600);
        var expectedWeeklyReset = beforeParse.AddSeconds(86400);

        Assert.True(result.FiveHourLimitResetTime >= expectedFiveHourReset.AddSeconds(-5) &&
                   result.FiveHourLimitResetTime <= expectedFiveHourReset.AddSeconds(5),
                   "Five hour reset time should be approximately 1 hour from now");

        Assert.True(result.WeeklyLimitResetTime >= expectedWeeklyReset.AddSeconds(-5) &&
                   result.WeeklyLimitResetTime <= expectedWeeklyReset.AddSeconds(5),
                   "Weekly reset time should be approximately 1 day from now");
    }

    [Fact]
    public void ParseUsageStats_ShouldParseWithHighUsage()
    {
        var sampleJsonl = MockCodexProcessRunner.CreateSampleJsonl(
            fiveHourPercent: 85.0,
            fiveHourResetsInSeconds: 7200, // 2 hours
            weeklyPercent: 50.0,
            weeklyResetsInSeconds: 172800 // 2 days
        );

        var method = typeof(CodexTuiService).GetMethod("ParseUsageStats",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method!.Invoke(_fixture.CodexTuiService, new object[] { sampleJsonl, 2 }) as CodexAuthManager.Core.Models.UsageStats;

        Assert.NotNull(result);
        Assert.Equal(2, result.IdentityId);
        Assert.Equal(85, result.FiveHourLimitPercent);
        Assert.Equal(50, result.WeeklyLimitPercent);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}
