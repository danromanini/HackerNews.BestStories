using System.ComponentModel.DataAnnotations;

namespace BestStories.Core.Stories;

public sealed class RefreshOptions
{
    public const string SectionName = "BestStories:Refresh";

    [Range(typeof(TimeSpan), "00:00:05", "1.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan ColdStartTimeout { get; set; } = TimeSpan.FromSeconds(15);

    [Range(1, 64)]
    public int MaxConcurrency { get; set; } = 8;

    [Range(1, BestStoriesLimits.MaxCount)]
    public int MaxStories { get; set; } = BestStoriesLimits.MaxCount;

    public TimeSpan StaleAfter => Interval * 3;
}
