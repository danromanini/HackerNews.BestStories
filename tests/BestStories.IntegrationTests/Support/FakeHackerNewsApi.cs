using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BestStories.IntegrationTests.Support;

public sealed record FakeStory(long Id, string Title, string By, int Score, int Comments, long Time, string? Url);

public sealed class FakeHackerNewsApi : HttpMessageHandler
{
    private const string BestStoriesPath = "/v0/beststories.json";
    private const string ItemPathPrefix = "/v0/item/";

    private readonly ConcurrentDictionary<long, FakeStory> _stories = new();
    private readonly ConcurrentDictionary<long, int> _itemRequests = new();
    private int _bestStoriesRequests;

    public TimeSpan Latency { get; set; } = TimeSpan.Zero;

    public int BestStoriesRequests => Volatile.Read(ref _bestStoriesRequests);

    public IReadOnlyDictionary<long, int> ItemRequests => _itemRequests;

    public IReadOnlyCollection<FakeStory> Stories => _stories.Values.ToArray();

    public FakeHackerNewsApi WithStory(FakeStory story)
    {
        ArgumentNullException.ThrowIfNull(story);

        _stories[story.Id] = story;
        return this;
    }

    public void UpdateScore(long id, int score) => _stories[id] = _stories[id] with { Score = score };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Latency > TimeSpan.Zero)
        {
            await Task.Delay(Latency, cancellationToken);
        }

        var path = request.RequestUri!.AbsolutePath;

        if (path == BestStoriesPath)
        {
            Interlocked.Increment(ref _bestStoriesRequests);
            return Json(_stories.Keys.Order().ToArray());
        }

        if (path.StartsWith(ItemPathPrefix, StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal))
        {
            var id = long.Parse(path[ItemPathPrefix.Length..^".json".Length], CultureInfo.InvariantCulture);
            _itemRequests.AddOrUpdate(id, 1, (_, count) => count + 1);

            return _stories.TryGetValue(id, out var story)
                ? Json(new
                {
                    id = story.Id,
                    type = "story",
                    by = story.By,
                    time = story.Time,
                    title = story.Title,
                    url = story.Url,
                    score = story.Score,
                    descendants = story.Comments,
                })
                : Json<object?>(null);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json"),
        };
}
