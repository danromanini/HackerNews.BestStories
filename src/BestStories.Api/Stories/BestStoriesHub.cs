using BestStories.Core.Stories;
using Microsoft.AspNetCore.SignalR;

namespace BestStories.Api.Stories;

public interface IBestStoriesHubClient
{
    Task BestStoriesUpdated(BestStoriesUpdate update);
}

internal sealed class BestStoriesHub(IBestStoriesService bestStoriesService) : Hub<IBestStoriesHubClient>
{
    public const string Path = "/hubs/best-stories";

    public async Task<BestStoriesUpdate> GetBestStories(int count)
    {
        if (count is < BestStoriesLimits.MinCount or > BestStoriesLimits.MaxCount)
        {
            throw new HubException($"count must be between {BestStoriesLimits.MinCount} and {BestStoriesLimits.MaxCount}.");
        }

        var result = await bestStoriesService.GetBestStoriesAsync(count, Context.ConnectionAborted)
            ?? throw new HubException("Best stories are not available yet.");

        return new BestStoriesUpdate(result.Version, result.RefreshedAt, BestStoryResponse.From(result.Stories));
    }
}
