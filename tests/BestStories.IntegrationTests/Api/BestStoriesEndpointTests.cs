using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BestStories.Api.Stories;
using BestStories.IntegrationTests.Support;

namespace BestStories.IntegrationTests.Api;

public class BestStoriesEndpointTests
{
    private BestStoriesApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var hackerNews = new FakeHackerNewsApi()
            .WithStory(new FakeStory(21233041, "A uBlock Origin update was rejected from the Chrome Web Store", "ismaildonmez", 1716, 572, 1570887781, "https://github.com/uBlockOrigin/uBlock-issues/issues/745"))
            .WithStory(new FakeStory(2, "Second", "bob", 900, 12, 1570000000, "https://example.com/2"))
            .WithStory(new FakeStory(3, "Third", "carol", 1200, 40, 1570000100, null));

        _factory = new BestStoriesApiFactory(hackerNews);
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        _factory.HackerNews.Dispose();
    }

    [Test]
    public async Task ReturnsTheBestStoriesInTheContractShapeOrderedByScore()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/stories/best?count=2", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stories = json.RootElement.EnumerateArray().ToArray();
        stories.Length.ShouldBe(2);

        var first = stories[0];
        first.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["title", "uri", "postedBy", "time", "score", "commentCount"]);
        first.GetProperty("title").GetString().ShouldBe("A uBlock Origin update was rejected from the Chrome Web Store");
        first.GetProperty("uri").GetString().ShouldBe("https://github.com/uBlockOrigin/uBlock-issues/issues/745");
        first.GetProperty("postedBy").GetString().ShouldBe("ismaildonmez");
        first.GetProperty("time").GetString().ShouldBe("2019-10-12T13:43:01+00:00");
        first.GetProperty("score").GetInt32().ShouldBe(1716);
        first.GetProperty("commentCount").GetInt32().ShouldBe(572);

        stories[1].GetProperty("title").GetString().ShouldBe("Third");
        stories[1].GetProperty("uri").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task ReturnsEveryAvailableStoryWhenMoreAreRequestedThanExist()
    {
        var stories = await _client.GetFromJsonAsync<BestStoryResponse[]>(new Uri("/api/v1/stories/best?count=200", UriKind.Relative));

        stories.ShouldNotBeNull();
        stories.Select(story => story.Score).ShouldBe([1716, 1200, 900]);
    }

    [TestCase("0")]
    [TestCase("-3")]
    [TestCase("201")]
    public async Task RejectsCountsOutsideTheAllowedRangeWithValidationProblem(string count)
    {
        var response = await _client.GetAsync(new Uri($"/api/v1/stories/best?count={count}", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("count", out _).ShouldBeTrue();
    }

    [TestCase("")]
    [TestCase("?count=abc")]
    public async Task RejectsMissingOrMalformedCounts(string query)
    {
        var response = await _client.GetAsync(new Uri($"/api/v1/stories/best{query}", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SupportsConditionalRequestsThroughETags()
    {
        var first = await _client.GetAsync(new Uri("/api/v1/stories/best?count=3", UriKind.Relative));
        var etag = first.Headers.ETag;
        etag.ShouldNotBeNull();
        first.Headers.CacheControl!.Public.ShouldBeTrue();
        first.Headers.CacheControl.MaxAge.ShouldNotBeNull();

        using var conditional = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/stories/best?count=3", UriKind.Relative));
        conditional.Headers.IfNoneMatch.Add(etag);
        var second = await _client.SendAsync(conditional);

        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await second.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task IssuesTheSameETagFromIndependentInstancesServingTheSameData()
    {
        using var otherHackerNews = new FakeHackerNewsApi();
        foreach (var story in _factory.HackerNews.Stories)
        {
            otherHackerNews.WithStory(story);
        }

        await using var otherInstance = new BestStoriesApiFactory(otherHackerNews);
        using var otherClient = otherInstance.CreateClient();
        var url = new Uri("/api/v1/stories/best?count=3", UriKind.Relative);

        var fromThisInstance = await _client.GetAsync(url);
        var fromOtherInstance = await otherClient.GetAsync(url);

        fromOtherInstance.Headers.ETag.ShouldBe(fromThisInstance.Headers.ETag);
    }

    [Test]
    public async Task DoesNotReturnNotModifiedForADifferentRepresentation()
    {
        var first = await _client.GetAsync(new Uri("/api/v1/stories/best?count=3", UriKind.Relative));

        using var conditional = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/stories/best?count=1", UriKind.Relative));
        conditional.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await _client.SendAsync(conditional);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("/health/live")]
    [TestCase("/health/ready")]
    public async Task ExposesHealthEndpoints(string path)
    {
        await _client.GetAsync(new Uri("/api/v1/stories/best?count=1", UriKind.Relative));

        var response = await _client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Test]
    public async Task PublishesAnOpenApiDocument()
    {
        var document = await _client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        document.ShouldContain("/api/v1/stories/best");
        document.ShouldContain("Hacker News Best Stories API");
    }

    [Test]
    public async Task ServesManyConcurrentCallersWithoutCallingHackerNewsAgain()
    {
        await _client.GetAsync(new Uri("/api/v1/stories/best?count=1", UriKind.Relative));
        var idRequestsBefore = _factory.HackerNews.BestStoriesRequests;

        var responses = await Task.WhenAll(Enumerable.Range(0, 1_000)
            .Select(_ => _client.GetAsync(new Uri("/api/v1/stories/best?count=3", UriKind.Relative))));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
        _factory.HackerNews.BestStoriesRequests.ShouldBe(idRequestsBefore);
    }
}
