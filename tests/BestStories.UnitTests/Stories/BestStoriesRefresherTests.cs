using BestStories.Core.Abstractions;
using BestStories.Core.Stories;
using BestStories.Infrastructure.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using static BestStories.UnitTests.TestData;

namespace BestStories.UnitTests.Stories;

public class BestStoriesRefresherTests
{
    private Mock<IHackerNewsClient> _hackerNews = null!;
    private Mock<IStorySnapshotObserver> _observer = null!;
    private InMemoryStorySnapshotStore _store = null!;
    private FakeTimeProvider _time = null!;
    private RefreshOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _hackerNews = new Mock<IHackerNewsClient>(MockBehavior.Strict);
        _observer = new Mock<IStorySnapshotObserver>();
        _store = new InMemoryStorySnapshotStore();
        _time = new FakeTimeProvider(Now);
        _options = new RefreshOptions { MaxConcurrency = 4, MaxStories = 200 };
    }

    [Test]
    public async Task RefreshAsync_PublishesASnapshotOrderedByScore()
    {
        GivenBestStories(Story(1, 10), Story(2, 30), Story(3, 20));

        var outcome = await CreateRefresher().RefreshAsync(CancellationToken.None);

        outcome.ShouldBe(RefreshOutcome.Updated);
        _store.Current.Version.ShouldBe(1);
        _store.Current.RefreshedAt.ShouldBe(Now);
        _store.Current.Stories.Select(story => story.Id).ShouldBe([2, 3, 1]);
    }

    [Test]
    public async Task RefreshAsync_FetchesAtMostMaxStories()
    {
        _options.MaxStories = 2;
        GivenBestStories(Story(1, 10), Story(2, 30), Story(3, 20));

        await CreateRefresher().RefreshAsync(CancellationToken.None);

        _store.Current.Stories.Length.ShouldBe(2);
        _hackerNews.Verify(client => client.GetStoryAsync(3, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RefreshAsync_FetchesEachStoryOnceEvenWhenIdsAreDuplicated()
    {
        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1, 1, 2]);
        _hackerNews.Setup(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, 10));
        _hackerNews.Setup(client => client.GetStoryAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Story(2, 20));

        await CreateRefresher().RefreshAsync(CancellationToken.None);

        _store.Current.Stories.Length.ShouldBe(2);
        _hackerNews.Verify(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RefreshAsync_SkipsItemsThatAreNoLongerStories()
    {
        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1, 2]);
        _hackerNews.Setup(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, 10));
        _hackerNews.Setup(client => client.GetStoryAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((Story?)null);

        await CreateRefresher().RefreshAsync(CancellationToken.None);

        _store.Current.Stories.Select(story => story.Id).ShouldBe([1]);
    }

    [Test]
    public async Task RefreshAsync_KeepsThePreviousSnapshotWhenTheIdsCannotBeFetched()
    {
        var previous = StorySnapshot.Create([Story(1, 10)], version: 7, Now.AddMinutes(-1));
        _store.Publish(previous);
        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var outcome = await CreateRefresher().RefreshAsync(CancellationToken.None);

        outcome.ShouldBe(RefreshOutcome.Failed);
        _store.Current.ShouldBeSameAs(previous);
    }

    [Test]
    public async Task RefreshAsync_FallsBackToTheLastKnownVersionOfAStoryThatFailedToLoad()
    {
        _store.Publish(StorySnapshot.Create([Story(1, 10), Story(2, 20)], version: 1, Now.AddMinutes(-1)));
        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1, 2]);
        _hackerNews.Setup(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, 50));
        _hackerNews.Setup(client => client.GetStoryAsync(2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var outcome = await CreateRefresher().RefreshAsync(CancellationToken.None);

        outcome.ShouldBe(RefreshOutcome.Updated);
        _store.Current.Stories.ShouldBe([Story(1, 50), Story(2, 20)]);
    }

    [Test]
    public async Task RefreshAsync_FailsWithoutPublishingWhenNoStoryCouldBeLoaded()
    {
        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([1, 2]);
        _hackerNews.Setup(client => client.GetStoryAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var outcome = await CreateRefresher().RefreshAsync(CancellationToken.None);

        outcome.ShouldBe(RefreshOutcome.Failed);
        _store.Current.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public async Task RefreshAsync_NotifiesObserversOnlyWhenTheStoriesChange()
    {
        GivenBestStories(Story(1, 10), Story(2, 20));
        var refresher = CreateRefresher();

        await refresher.RefreshAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(1));
        var secondOutcome = await refresher.RefreshAsync(CancellationToken.None);

        secondOutcome.ShouldBe(RefreshOutcome.Unchanged);
        _store.Current.Version.ShouldBe(1);
        _store.Current.RefreshedAt.ShouldBe(Now.AddMinutes(1));
        _observer.Verify(
            observer => observer.OnSnapshotChangedAsync(It.Is<StorySnapshot>(snapshot => snapshot.Version == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RefreshAsync_BumpsTheVersionWhenAScoreChanges()
    {
        GivenBestStories(Story(1, 10), Story(2, 20));
        var refresher = CreateRefresher();
        await refresher.RefreshAsync(CancellationToken.None);

        _hackerNews.Setup(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, 99));
        var outcome = await refresher.RefreshAsync(CancellationToken.None);

        outcome.ShouldBe(RefreshOutcome.Updated);
        _store.Current.Version.ShouldBe(2);
        _store.Current.Stories[0].ShouldBe(Story(1, 99));
    }

    [Test]
    public async Task RefreshAsync_StillPublishesWhenAnObserverFails()
    {
        GivenBestStories(Story(1, 10));
        _observer.Setup(observer => observer.OnSnapshotChangedAsync(It.IsAny<StorySnapshot>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        var outcome = await CreateRefresher().RefreshAsync(CancellationToken.None);

        outcome.ShouldBe(RefreshOutcome.Updated);
        _store.Current.IsEmpty.ShouldBeFalse();
    }

    [Test]
    public async Task RefreshAsync_NeverExceedsTheConfiguredConcurrency()
    {
        _options.MaxConcurrency = 3;
        var ids = Enumerable.Range(1, 30).Select(id => (long)id).ToArray();
        var inFlight = 0;
        var peak = 0;

        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ids);
        _hackerNews.Setup(client => client.GetStoryAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(async (long id, CancellationToken token) =>
            {
                var current = Interlocked.Increment(ref inFlight);
                InterlockedMax(ref peak, current);
                await Task.Delay(TimeSpan.FromMilliseconds(15), token);
                Interlocked.Decrement(ref inFlight);
                return Story(id, (int)id);
            });

        await CreateRefresher().RefreshAsync(CancellationToken.None);

        _store.Current.Stories.Length.ShouldBe(30);
        peak.ShouldBeInRange(1, 3);
    }

    [Test]
    public async Task RefreshAsync_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() => CreateRefresher().RefreshAsync(cancellation.Token));
    }

    private void GivenBestStories(params Story[] stories)
    {
        _hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stories.Select(story => story.Id).ToArray());

        foreach (var story in stories)
        {
            _hackerNews.Setup(client => client.GetStoryAsync(story.Id, It.IsAny<CancellationToken>())).ReturnsAsync(story);
        }
    }

    private BestStoriesRefresher CreateRefresher() =>
        new(_hackerNews.Object, _store, [_observer.Object], Monitor(_options), _time, NullLogger<BestStoriesRefresher>.Instance);

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
