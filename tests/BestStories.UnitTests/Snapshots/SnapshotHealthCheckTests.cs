using BestStories.Core.Stories;
using BestStories.Infrastructure.Snapshots;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using static BestStories.UnitTests.TestData;

namespace BestStories.UnitTests.Snapshots;

public class SnapshotHealthCheckTests
{
    private InMemoryStorySnapshotStore _store = null!;
    private FakeTimeProvider _time = null!;
    private SnapshotHealthCheck _healthCheck = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new InMemoryStorySnapshotStore();
        _time = new FakeTimeProvider(Now);
        _healthCheck = new SnapshotHealthCheck(_store, Monitor(new RefreshOptions { Interval = TimeSpan.FromMinutes(1) }), _time);
    }

    [Test]
    public async Task IsUnhealthyUntilTheFirstSnapshotIsLoaded()
    {
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task IsHealthyWhileTheSnapshotIsFresh()
    {
        _store.Publish(StorySnapshot.Create([Story(1, 10)], version: 1, Now));
        _time.Advance(TimeSpan.FromMinutes(2));

        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Test]
    public async Task IsDegradedWhenTheSnapshotIsStaleButStillServesData()
    {
        _store.Publish(StorySnapshot.Create([Story(1, 10)], version: 1, Now));
        _time.Advance(TimeSpan.FromMinutes(4));

        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Data["stories"].ShouldBe(1);
    }
}
