namespace CodexAuthManager.Core.Models;

/// <summary>
/// Represents Codex usage statistics for an identity
/// </summary>
public class UsageStats
{
    public int Id { get; set; }
    public int IdentityId { get; set; }

    /// <summary>
    /// 5-hour limit usage percentage (0-100)
    /// </summary>
    public int FiveHourLimitPercent { get; set; }

    /// <summary>
    /// When the 5-hour limit resets next
    /// </summary>
    public DateTime FiveHourLimitResetTime { get; set; }

    /// <summary>
    /// Weekly limit usage percentage (0-100)
    /// </summary>
    public int WeeklyLimitPercent { get; set; }

    /// <summary>
    /// When the weekly limit resets next
    /// </summary>
    public DateTime WeeklyLimitResetTime { get; set; }

    /// <summary>
    /// When these stats were captured
    /// </summary>
    public DateTime CapturedAt { get; set; }

    public UsageStats()
    {
        CapturedAt = DateTime.UtcNow;
    }
}
