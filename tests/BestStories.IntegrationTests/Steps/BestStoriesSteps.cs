using System.Globalization;
using System.Net.Http.Json;
using BestStories.Api.Stories;
using BestStories.IntegrationTests.Support;
using Reqnroll;

namespace BestStories.IntegrationTests.Steps;

[Binding]
public sealed class BestStoriesSteps(ApiDriver driver)
{
    private readonly List<HttpResponseMessage> _responses = [];

    private HttpResponseMessage LastResponse => _responses[^1];

    [Given("Hacker News has the following best stories:")]
    public void GivenHackerNewsHasTheFollowingBestStories(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            driver.HackerNews.WithStory(new FakeStory(
                long.Parse(row["id"], CultureInfo.InvariantCulture),
                row["title"],
                row["by"],
                int.Parse(row["score"], CultureInfo.InvariantCulture),
                int.Parse(row["comments"], CultureInfo.InvariantCulture),
                long.Parse(row["time"], CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(row["url"]) ? null : row["url"]));
        }
    }

    [Given("Hacker News takes {int} milliseconds to respond")]
    public void GivenHackerNewsTakesMillisecondsToRespond(int milliseconds) =>
        driver.HackerNews.Latency = TimeSpan.FromMilliseconds(milliseconds);

    [Given("each client may make {int} requests")]
    public void GivenEachClientMayMakeRequests(int permits)
    {
        driver.Configure("BestStories:RateLimiting:PermitLimit", permits.ToString(CultureInfo.InvariantCulture));
        driver.Configure("BestStories:RateLimiting:Window", "00:10:00");
    }

    [When("I request the best {int} stories")]
    public async Task WhenIRequestTheBestStories(int count) =>
        _responses.Add(await driver.Client.GetAsync(BestStoriesUri(count)));

    [When("I request the best {int} stories {int} times")]
    public async Task WhenIRequestTheBestStoriesTimes(int count, int times)
    {
        for (var i = 0; i < times; i++)
        {
            _responses.Add(await driver.Client.GetAsync(BestStoriesUri(count)));
        }
    }

    [When("{int} clients request the best {int} stories at the same time")]
    public async Task WhenClientsRequestTheBestStoriesAtTheSameTime(int clients, int count)
    {
        var client = driver.Client;
        var responses = await Task.WhenAll(Enumerable.Range(0, clients).Select(_ => client.GetAsync(BestStoriesUri(count))));
        _responses.AddRange(responses);
    }

    [Then("the response status should be {int}")]
    [Then("the last response status should be {int}")]
    public void ThenTheResponseStatusShouldBe(int status) => ((int)LastResponse.StatusCode).ShouldBe(status);

    [Then("every response status should be {int}")]
    public void ThenEveryResponseStatusShouldBe(int status) =>
        _responses.ShouldAllBe(response => (int)response.StatusCode == status);

    [Then("the stories should be returned in this order:")]
    public async Task ThenTheStoriesShouldBeReturnedInThisOrder(DataTable table)
    {
        var stories = await LastResponse.Content.ReadFromJsonAsync<BestStoryResponse[]>();

        stories.ShouldNotBeNull();
        stories.Length.ShouldBe(table.RowCount);
        for (var i = 0; i < table.RowCount; i++)
        {
            var expected = table.Rows[i];
            var actual = stories[i];
            actual.ShouldSatisfyAllConditions(
                () => actual.Title.ShouldBe(expected["title"]),
                () => actual.PostedBy.ShouldBe(expected["postedBy"]),
                () => actual.Score.ShouldBe(int.Parse(expected["score"], CultureInfo.InvariantCulture)),
                () => actual.CommentCount.ShouldBe(int.Parse(expected["commentCount"], CultureInfo.InvariantCulture)),
                () => actual.Uri.ShouldBe(string.IsNullOrWhiteSpace(expected["uri"]) ? null : expected["uri"]),
                () => actual.Time.ShouldBe(DateTimeOffset.Parse(expected["time"], CultureInfo.InvariantCulture)));
        }
    }

    [Then("{int} stories should be returned")]
    public async Task ThenStoriesShouldBeReturned(int count)
    {
        var stories = await LastResponse.Content.ReadFromJsonAsync<BestStoryResponse[]>();

        stories.ShouldNotBeNull();
        stories.Length.ShouldBe(count);
    }

    [Then("Hacker News should have been asked for the best story ids {int} time(s)")]
    public void ThenHackerNewsShouldHaveBeenAskedForTheBestStoryIds(int times) =>
        driver.HackerNews.BestStoriesRequests.ShouldBe(times);

    [Then("each story should have been fetched from Hacker News exactly once")]
    public void ThenEachStoryShouldHaveBeenFetchedExactlyOnce()
    {
        var requests = driver.HackerNews.ItemRequests;

        requests.Keys.Order().ShouldBe(driver.HackerNews.Stories.Select(story => story.Id).Order());
        requests.Values.ShouldAllBe(count => count == 1);
    }

    private static Uri BestStoriesUri(int count) =>
        new(string.Create(CultureInfo.InvariantCulture, $"/api/v1/stories/best?count={count}"), UriKind.Relative);
}
