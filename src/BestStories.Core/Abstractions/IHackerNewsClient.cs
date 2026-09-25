using BestStories.Core.Stories;

namespace BestStories.Core.Abstractions;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken);

    Task<Story?> GetStoryAsync(long id, CancellationToken cancellationToken);
}
