using System.Collections.Immutable;
using BestStories.Core.Stories;

namespace BestStories.Api.Stories;

public sealed record BestStoryResponse(
    string Title,
    string? Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount)
{
    public static BestStoryResponse From(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        return new(story.Title, story.Uri, story.PostedBy, story.Time, story.Score, story.CommentCount);
    }

    public static BestStoryResponse[] From(ImmutableArray<Story> stories)
    {
        var responses = new BestStoryResponse[stories.Length];
        for (var i = 0; i < stories.Length; i++)
        {
            responses[i] = From(stories[i]);
        }

        return responses;
    }
}

public sealed record BestStoriesUpdate(long Version, DateTimeOffset RefreshedAt, BestStoryResponse[] Stories);
