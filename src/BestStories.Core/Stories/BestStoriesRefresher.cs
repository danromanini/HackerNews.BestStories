using System.Collections.Concurrent;
using BestStories.Core.Abstractions;
using BestStories.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BestStories.Core.Stories;

public sealed partial class BestStoriesRefresher(
    IHackerNewsClient hackerNewsClient,
    IStorySnapshotStore snapshotStore,
    IEnumerable<IStorySnapshotObserver> observers,
    IOptionsMonitor<RefreshOptions> options,
    TimeProvider timeProvider,
    ILogger<BestStoriesRefresher> logger)
{
    public async Task<RefreshOutcome> RefreshAsync(CancellationToken cancellationToken)
    {
        using var activity = BestStoriesTelemetry.ActivitySource.StartActivity("best-stories.refresh");
        var startedAt = timeProvider.GetTimestamp();

        var outcome = await RefreshCoreAsync(cancellationToken);

        var elapsed = timeProvider.GetElapsedTime(startedAt);
        BestStoriesTelemetry.RecordRefresh(outcome, elapsed);
        activity?.SetTag("beststories.refresh.outcome", outcome.ToString());
        LogRefreshCompleted(outcome, snapshotStore.Current.Version, elapsed.TotalMilliseconds);

        return outcome;
    }

    private async Task<RefreshOutcome> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var previous = snapshotStore.Current;

        IReadOnlyList<long> ids;
        try
        {
            ids = await hackerNewsClient.GetBestStoryIdsAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogIdsFetchFailed(exception);
            return RefreshOutcome.Failed;
        }

        var wanted = ids.Distinct().Take(settings.MaxStories).ToArray();
        var stories = await FetchStoriesAsync(wanted, previous, settings.MaxConcurrency, cancellationToken);

        if (stories.Count == 0 && wanted.Length > 0)
        {
            LogNoStoriesFetched(wanted.Length);
            return RefreshOutcome.Failed;
        }

        var now = timeProvider.GetUtcNow();
        var candidate = StorySnapshot.Create(stories, previous.Version + 1, now);

        if (!previous.IsEmpty && candidate.HasSameStoriesAs(previous))
        {
            snapshotStore.Publish(previous with { RefreshedAt = now });
            return RefreshOutcome.Unchanged;
        }

        snapshotStore.Publish(candidate);
        await NotifyObserversAsync(candidate, cancellationToken);

        return RefreshOutcome.Updated;
    }

    private async Task<IReadOnlyCollection<Story>> FetchStoriesAsync(
        long[] ids,
        StorySnapshot previous,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        var fallback = previous.Stories.ToDictionary(story => story.Id);
        var stories = new ConcurrentBag<Story>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(ids, parallelOptions, async (id, token) =>
        {
            try
            {
                var story = await hackerNewsClient.GetStoryAsync(id, token);
                if (story is not null)
                {
                    stories.Add(story);
                }
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                BestStoriesTelemetry.RecordStoryFetchFailure();
                LogStoryFetchFailed(id, exception);

                if (fallback.TryGetValue(id, out var stale))
                {
                    stories.Add(stale);
                }
            }
        });

        return stories;
    }

    private async Task NotifyObserversAsync(StorySnapshot snapshot, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.OnSnapshotChangedAsync(snapshot, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogObserverFailed(observer.GetType().Name, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Best stories refresh finished with outcome {Outcome} (snapshot version {Version}) in {ElapsedMilliseconds:F0} ms")]
    private partial void LogRefreshCompleted(RefreshOutcome outcome, long version, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not fetch best story ids from Hacker News; keeping the previous snapshot")]
    private partial void LogIdsFetchFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not fetch story {StoryId} from Hacker News")]
    private partial void LogStoryFetchFailed(long storyId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "None of the {Requested} best stories could be fetched; keeping the previous snapshot")]
    private partial void LogNoStoriesFetched(int requested);

    [LoggerMessage(Level = LogLevel.Error, Message = "Snapshot observer {Observer} failed")]
    private partial void LogObserverFailed(string observer, Exception exception);
}
