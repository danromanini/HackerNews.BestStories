using BestStories.Core.Stories;
using BestStories.Infrastructure.Snapshots;
using Microsoft.Extensions.Time.Testing;
using static BestStories.UnitTests.TestData;

namespace BestStories.UnitTests.Stories;

public class BestStoriesServiceTests
{
    private InMemoryStorySnapshotStore _store = null!;
    private FakeTimeProvider _time = null!;
    private BestStoriesService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new InMemoryStorySnapshotStore();
        _time = new FakeTimeProvider(Now);
        var options = new RefreshOptions { Interval = TimeSpan.FromMinutes(1), ColdStartTimeout = TimeSpan.FromSeconds(10) };
        _service = new BestStoriesService(_store, Monitor(options), _time);
    }

    [Test]
    public async Task GetBestStoriesAsync_ReturnsTheTopNStoriesFromTheCurrentSnapshot()
    {
        _store.Publish(StorySnapshot.Create([Story(1, 10), Story(2, 30), Story(3, 20)], version: 4, Now));

        var result = await _service.GetBestStoriesAsync(2, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Stories.Select(story => story.Id).ShouldBe([2, 3]);
        result.Version.ShouldBe(4);
        result.NextRefreshAt.ShouldBe(Now.AddMinutes(1));
    }

    [Test]
    public void GetBestStoriesAsync_CompletesSynchronouslyWhenTheSnapshotIsWarm()
    {
        _store.Publish(StorySnapshot.Create([Story(1, 10)], version: 1, Now));

        var pending = _service.GetBestStoriesAsync(1, CancellationToken.None);

        pending.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Test]
    public async Task GetBestStoriesAsync_WaitsForTheFirstSnapshotOnColdStart()
    {
        var pending = _service.GetBestStoriesAsync(1, CancellationToken.None).AsTask();
        pending.IsCompleted.ShouldBeFalse();

        _store.Publish(StorySnapshot.Create([Story(1, 10)], version: 1, Now));

        var result = await pending;
        result.ShouldNotBeNull();
        result.Stories.Length.ShouldBe(1);
    }

    [Test]
    public async Task GetBestStoriesAsync_ReturnsNullWhenTheColdStartTimeoutElapses()
    {
        var pending = _service.GetBestStoriesAsync(1, CancellationToken.None).AsTask();

        _time.Advance(TimeSpan.FromSeconds(11));

        (await pending).ShouldBeNull();
    }
}
