using System.Text.Json;
using System.Text.Json.Serialization;
using BestStories.Core.Stories;

namespace BestStories.Infrastructure.HackerNews;

internal sealed record HackerNewsItem(
    long Id,
    string? Type,
    string? By,
    long Time,
    string? Title,
    string? Url,
    int Score,
    int Descendants,
    bool Deleted,
    bool Dead)
{
    private const string StoryType = "story";

    public Story? ToStory()
    {
        if (Deleted || Dead || Title is null || !string.Equals(Type, StoryType, StringComparison.Ordinal))
        {
            return null;
        }

        return new Story(
            Id,
            Title,
            Url,
            By ?? string.Empty,
            DateTimeOffset.FromUnixTimeSeconds(Time),
            Score,
            Descendants);
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HackerNewsItem))]
[JsonSerializable(typeof(long[]))]
internal sealed partial class HackerNewsJsonContext : JsonSerializerContext;
