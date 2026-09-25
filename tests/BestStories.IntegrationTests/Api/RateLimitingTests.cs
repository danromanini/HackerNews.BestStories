using System.Net;
using BestStories.IntegrationTests.Support;

namespace BestStories.IntegrationTests.Api;

public class RateLimitingTests
{
    [Test]
    public async Task RejectsClientsThatExceedTheirQuotaWithProblemDetailsAndRetryAfter()
    {
        using var hackerNews = new FakeHackerNewsApi().WithStory(new FakeStory(1, "One", "alice", 100, 1, 1570000000, null));
        await using var factory = new BestStoriesApiFactory(hackerNews, new Dictionary<string, string?>
        {
            ["BestStories:RateLimiting:PermitLimit"] = "3",
            ["BestStories:RateLimiting:Window"] = "00:10:00",
        });
        using var client = factory.CreateClient();
        var url = new Uri("/api/v1/stories/best?count=1", UriKind.Relative);

        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var rejected = await client.GetAsync(url);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await client.GetAsync(new Uri("/health/live", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
