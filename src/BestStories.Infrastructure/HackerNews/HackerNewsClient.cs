using System.Globalization;
using System.Net.Http.Json;
using BestStories.Core.Abstractions;
using BestStories.Core.Stories;

namespace BestStories.Infrastructure.HackerNews;

internal sealed class HackerNewsClient(HttpClient httpClient) : IHackerNewsClient
{
    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await httpClient.GetFromJsonAsync(
            "beststories.json",
            HackerNewsJsonContext.Default.Int64Array,
            cancellationToken);

        return ids ?? [];
    }

    public async Task<Story?> GetStoryAsync(long id, CancellationToken cancellationToken)
    {
        var item = await httpClient.GetFromJsonAsync(
            string.Create(CultureInfo.InvariantCulture, $"item/{id}.json"),
            HackerNewsJsonContext.Default.HackerNewsItem,
            cancellationToken);

        return item?.ToStory();
    }
}
