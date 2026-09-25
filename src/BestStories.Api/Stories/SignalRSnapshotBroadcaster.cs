using BestStories.Core.Abstractions;
using BestStories.Core.Stories;
using Microsoft.AspNetCore.SignalR;

namespace BestStories.Api.Stories;

internal sealed class SignalRSnapshotBroadcaster(IHubContext<BestStoriesHub, IBestStoriesHubClient> hubContext)
    : IStorySnapshotObserver
{
    public Task OnSnapshotChangedAsync(StorySnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var update = new BestStoriesUpdate(snapshot.Version, snapshot.RefreshedAt, BestStoryResponse.From(snapshot.Stories));

        return hubContext.Clients.All.BestStoriesUpdated(update);
    }
}
