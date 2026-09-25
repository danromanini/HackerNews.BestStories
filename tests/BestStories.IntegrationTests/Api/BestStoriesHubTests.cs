using BestStories.Api.Stories;
using BestStories.Core.Stories;
using BestStories.IntegrationTests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace BestStories.IntegrationTests.Api;

public class BestStoriesHubTests
{
    private BestStoriesApiFactory _factory = null!;
    private HubConnection _connection = null!;

    [SetUp]
    public async Task SetUp()
    {
        var hackerNews = new FakeHackerNewsApi()
            .WithStory(new FakeStory(1, "One", "alice", 100, 1, 1570000000, "https://example.com/1"))
            .WithStory(new FakeStory(2, "Two", "bob", 200, 2, 1570000000, "https://example.com/2"));

        _factory = new BestStoriesApiFactory(hackerNews);
        _connection = _factory.CreateHubConnection();
        await _connection.StartAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _connection.DisposeAsync();
        await _factory.DisposeAsync();
        _factory.HackerNews.Dispose();
    }

    [Test]
    public async Task ClientsCanQueryTheBestStories()
    {
        var update = await _connection.InvokeAsync<BestStoriesUpdate>("GetBestStories", 1);

        update.Stories.Select(story => story.Title).ShouldBe(["Two"]);
        update.Version.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task InvalidCountsAreRejected()
    {
        var error = await Should.ThrowAsync<HubException>(() => _connection.InvokeAsync<BestStoriesUpdate>("GetBestStories", 0));

        error.Message.ShouldContain("count must be between 1 and 200");
    }

    [Test]
    public async Task ClientsArePushedANewSnapshotWhenScoresChange()
    {
        var initial = await _connection.InvokeAsync<BestStoriesUpdate>("GetBestStories", 2);
        var received = new TaskCompletionSource<BestStoriesUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = _connection.On<BestStoriesUpdate>(nameof(IBestStoriesHubClient.BestStoriesUpdated), update =>
        {
            if (update.Version > initial.Version)
            {
                received.TrySetResult(update);
            }
        });

        _factory.HackerNews.UpdateScore(1, 500);
        var outcome = await _factory.Services.GetRequiredService<BestStoriesRefresher>().RefreshAsync(CancellationToken.None);

        outcome.ShouldBe(RefreshOutcome.Updated);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        pushed.Version.ShouldBe(initial.Version + 1);
        pushed.Stories.Select(story => (story.Title, story.Score)).ShouldBe([("One", 500), ("Two", 200)]);
    }
}
