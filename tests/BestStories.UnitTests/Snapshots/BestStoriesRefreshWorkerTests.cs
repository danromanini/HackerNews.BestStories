using BestStories.Core.Abstractions;
using BestStories.Core.Stories;
using BestStories.Infrastructure.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using static BestStories.UnitTests.TestData;

namespace BestStories.UnitTests.Snapshots;

public class BestStoriesRefreshWorkerTests
{
    private readonly RefreshOptions _options = new()
    {
        Interval = TimeSpan.FromMinutes(1),
        RetryDelay = TimeSpan.FromSeconds(5),
    };

    [Test]
    public async Task RetriesQuicklyWhileNoSnapshotHasBeenLoaded()
    {
        var hackerNews = new Mock<IHackerNewsClient>();
        var idCalls = 0;
        hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref idCalls) == 1
                ? Task.FromException<IReadOnlyList<long>>(new HttpRequestException("boom"))
                : Task.FromResult<IReadOnlyList<long>>([1]));
        hackerNews.Setup(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, 10));

        var (worker, store, time) = CreateWorker(hackerNews.Object);
        await worker.StartAsync(CancellationToken.None);

        await WaitUntilAsync(() => Volatile.Read(ref idCalls) == 1);
        store.Current.IsEmpty.ShouldBeTrue();

        await AdvanceUntilAsync(time, _options.RetryDelay, () => !store.Current.IsEmpty);

        Volatile.Read(ref idCalls).ShouldBe(2);
        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();
    }

    [Test]
    public async Task RefreshesOnTheConfiguredIntervalOnceWarm()
    {
        var hackerNews = new Mock<IHackerNewsClient>();
        var idCalls = 0;
        hackerNews.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref idCalls))
            .ReturnsAsync([1]);
        hackerNews.Setup(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Story(1, 10));

        var (worker, _, time) = CreateWorker(hackerNews.Object);
        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Volatile.Read(ref idCalls) == 1);

        time.Advance(_options.RetryDelay);
        await Task.Delay(50);
        Volatile.Read(ref idCalls).ShouldBe(1);

        await AdvanceUntilAsync(time, _options.Interval, () => Volatile.Read(ref idCalls) == 2);

        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();
    }

    private (BestStoriesRefreshWorker Worker, InMemoryStorySnapshotStore Store, FakeTimeProvider Time) CreateWorker(IHackerNewsClient hackerNews)
    {
        var store = new InMemoryStorySnapshotStore();
        var time = new FakeTimeProvider(Now);
        var options = Monitor(_options);
        var refresher = new BestStoriesRefresher(hackerNews, store, [], options, time, NullLogger<BestStoriesRefresher>.Instance);
        var worker = new BestStoriesRefreshWorker(refresher, store, options, time, NullLogger<BestStoriesRefreshWorker>.Instance);

        return (worker, store, time);
    }

    private static async Task AdvanceUntilAsync(FakeTimeProvider time, TimeSpan step, Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            time.Advance(step);
            await Task.Delay(10);
        }

        condition().ShouldBeTrue();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        condition().ShouldBeTrue();
    }
}
