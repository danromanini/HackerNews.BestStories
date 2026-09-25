using BestStories.Core.Abstractions;
using BestStories.Core.Stories;

namespace BestStories.Infrastructure.Snapshots;

internal sealed class InMemoryStorySnapshotStore : IStorySnapshotStore
{
    private readonly TaskCompletionSource<StorySnapshot> _firstSnapshot =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private StorySnapshot _current = StorySnapshot.Empty;

    public StorySnapshot Current => Volatile.Read(ref _current);

    public void Publish(StorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Volatile.Write(ref _current, snapshot);
        _firstSnapshot.TrySetResult(snapshot);
    }

    public Task<StorySnapshot> WaitForFirstSnapshotAsync(CancellationToken cancellationToken) =>
        _firstSnapshot.Task.WaitAsync(cancellationToken);
}
