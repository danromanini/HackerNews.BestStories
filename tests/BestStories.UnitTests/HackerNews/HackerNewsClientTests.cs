using System.Net;
using System.Text;
using BestStories.Infrastructure.HackerNews;

namespace BestStories.UnitTests.HackerNews;

public class HackerNewsClientTests
{
    private const string ItemJson = """
        {
          "by": "ismaildonmez",
          "descendants": 572,
          "id": 21233041,
          "kids": [21233229, 21233577],
          "score": 1716,
          "time": 1570887781,
          "title": "A uBlock Origin update was rejected from the Chrome Web Store",
          "type": "story",
          "url": "https://github.com/uBlockOrigin/uBlock-issues/issues/745"
        }
        """;

    [Test]
    public async Task GetBestStoryIdsAsync_ReadsTheIdsFromBestStoriesEndpoint()
    {
        using var handler = new StubHandler(_ => "[3, 1, 2]");
        var client = CreateClient(handler);

        var ids = await client.GetBestStoryIdsAsync(CancellationToken.None);

        ids.ShouldBe([3L, 1L, 2L]);
        handler.RequestedPaths.ShouldBe(["/v0/beststories.json"]);
    }

    [Test]
    public async Task GetStoryAsync_ReadsAndMapsTheItem()
    {
        using var handler = new StubHandler(_ => ItemJson);
        var client = CreateClient(handler);

        var story = await client.GetStoryAsync(21233041, CancellationToken.None);

        story.ShouldNotBeNull();
        story.Score.ShouldBe(1716);
        story.CommentCount.ShouldBe(572);
        handler.RequestedPaths.ShouldBe(["/v0/item/21233041.json"]);
    }

    [Test]
    public async Task GetStoryAsync_ReturnsNullWhenHackerNewsReturnsNull()
    {
        using var handler = new StubHandler(_ => "null");
        var client = CreateClient(handler);

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        story.ShouldBeNull();
    }

    [Test]
    public async Task GetStoryAsync_SurfacesHttpFailuresToTheCaller()
    {
        using var handler = new StubHandler(_ => null);
        var client = CreateClient(handler);

        await Should.ThrowAsync<HttpRequestException>(() => client.GetStoryAsync(1, CancellationToken.None));
    }

    private static HackerNewsClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/") });

    private sealed class StubHandler(Func<string, string?> respond) : HttpMessageHandler
    {
        public List<string> RequestedPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            RequestedPaths.Add(path);

            var body = respond(path);
            var response = body is null
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            return Task.FromResult(response);
        }
    }
}
