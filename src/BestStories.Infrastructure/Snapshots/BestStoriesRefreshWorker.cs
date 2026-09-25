using BestStories.Core.Abstractions;
using BestStories.Core.Stories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BestStories.Infrastructure.Snapshots;

internal sealed partial class BestStoriesRefreshWorker(
    BestStoriesRefresher refresher,
    IStorySnapshotStore snapshotStore,
    IOptionsMonitor<RefreshOptions> options,
    TimeProvider timeProvider,
    ILogger<BestStoriesRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var outcome = await RefreshSafelyAsync(stoppingToken);
            var delay = NextDelay(outcome);

            await Task.Delay(delay, timeProvider, stoppingToken);
        }
    }

    private async Task<RefreshOutcome> RefreshSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            return await refresher.RefreshAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogUnexpectedFailure(exception);
            return RefreshOutcome.Failed;
        }
    }

    private TimeSpan NextDelay(RefreshOutcome outcome)
    {
        var settings = options.CurrentValue;

        return outcome == RefreshOutcome.Failed && snapshotStore.Current.IsEmpty
            ? settings.RetryDelay
            : settings.Interval;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected failure while refreshing best stories")]
    private partial void LogUnexpectedFailure(Exception exception);
}
