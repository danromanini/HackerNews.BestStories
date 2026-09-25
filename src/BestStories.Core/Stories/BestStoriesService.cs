using System.Collections.Immutable;
using BestStories.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace BestStories.Core.Stories;

public interface IBestStoriesService
{
    ValueTask<BestStoriesResult?> GetBestStoriesAsync(int count, CancellationToken cancellationToken);
}

public sealed record BestStoriesResult(
    ImmutableArray<Story> Stories,
    long Version,
    string ContentHash,
    DateTimeOffset RefreshedAt,
    DateTimeOffset NextRefreshAt);

public sealed class BestStoriesService(
    IStorySnapshotStore snapshotStore,
    IOptionsMonitor<RefreshOptions> options,
    TimeProvider timeProvider) : IBestStoriesService
{
    public ValueTask<BestStoriesResult?> GetBestStoriesAsync(int count, CancellationToken cancellationToken)
    {
        var snapshot = snapshotStore.Current;

        return snapshot.IsEmpty
            ? WaitForFirstSnapshotAsync(count, cancellationToken)
            : ValueTask.FromResult<BestStoriesResult?>(ToResult(snapshot, count));
    }

    private async ValueTask<BestStoriesResult?> WaitForFirstSnapshotAsync(int count, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await snapshotStore
                .WaitForFirstSnapshotAsync(cancellationToken)
                .WaitAsync(options.CurrentValue.ColdStartTimeout, timeProvider, cancellationToken);

            return ToResult(snapshot, count);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private BestStoriesResult ToResult(StorySnapshot snapshot, int count) =>
        new(snapshot.Top(count), snapshot.Version, snapshot.ContentHash, snapshot.RefreshedAt, snapshot.RefreshedAt + options.CurrentValue.Interval);
}
