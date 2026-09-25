using BestStories.Core.Stories;

namespace BestStories.Core.Abstractions;

public interface IStorySnapshotStore
{
    StorySnapshot Current { get; }

    void Publish(StorySnapshot snapshot);

    Task<StorySnapshot> WaitForFirstSnapshotAsync(CancellationToken cancellationToken);
}
