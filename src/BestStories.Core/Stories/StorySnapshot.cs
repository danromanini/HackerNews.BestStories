using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BestStories.Core.Stories;

public sealed record StorySnapshot(ImmutableArray<Story> Stories, long Version, DateTimeOffset RefreshedAt)
{
    public static StorySnapshot Empty { get; } = new([], 0, DateTimeOffset.MinValue);

    public string ContentHash { get; } = ComputeContentHash(Stories);

    public bool IsEmpty => Version == 0;

    public static StorySnapshot Create(IEnumerable<Story> stories, long version, DateTimeOffset refreshedAt)
    {
        ArgumentNullException.ThrowIfNull(stories);

        var ordered = stories
            .OrderByDescending(story => story.Score)
            .ThenBy(story => story.Id)
            .ToImmutableArray();

        return new StorySnapshot(ordered, version, refreshedAt);
    }

    public ImmutableArray<Story> Top(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return Stories.Slice(0, Math.Min(count, Stories.Length));
    }

    public bool HasSameStoriesAs(StorySnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Stories.AsSpan().SequenceEqual(other.Stories.AsSpan());
    }

    private static string ComputeContentHash(ImmutableArray<Story> stories)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var story in stories)
        {
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{story.Id}\u001f{story.Score}\u001f{story.CommentCount}\u001f{story.Time.ToUnixTimeSeconds()}\u001f{story.Title}\u001f{story.Uri}\u001f{story.PostedBy}\u001e");
            hash.AppendData(Encoding.UTF8.GetBytes(line));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset(), 0, 12);
    }
}
