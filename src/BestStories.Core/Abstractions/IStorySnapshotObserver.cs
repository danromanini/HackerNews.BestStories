using BestStories.Core.Stories;

namespace BestStories.Core.Abstractions;

public interface IStorySnapshotObserver
{
    Task OnSnapshotChangedAsync(StorySnapshot snapshot, CancellationToken cancellationToken);
}
