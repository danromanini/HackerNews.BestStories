using System.Diagnostics;
using System.Diagnostics.Metrics;
using BestStories.Core.Stories;

namespace BestStories.Core.Diagnostics;

public static class BestStoriesTelemetry
{
    public const string Name = "BestStories";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    private static readonly Counter<long> Refreshes = Meter.CreateCounter<long>(
        "beststories.refreshes",
        unit: "{refresh}",
        description: "Number of snapshot refreshes, tagged by outcome.");

    private static readonly Histogram<double> RefreshDuration = Meter.CreateHistogram<double>(
        "beststories.refresh.duration",
        unit: "s",
        description: "Time taken to rebuild the best stories snapshot.");

    private static readonly Counter<long> StoryFetchFailures = Meter.CreateCounter<long>(
        "beststories.story_fetch.failures",
        unit: "{failure}",
        description: "Individual story fetches that failed after resilience policies were exhausted.");

    internal static void RecordRefresh(RefreshOutcome outcome, TimeSpan elapsed)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome.ToString());
        Refreshes.Add(1, tag);
        RefreshDuration.Record(elapsed.TotalSeconds, tag);
    }

    internal static void RecordStoryFetchFailure() => StoryFetchFailures.Add(1);
}
