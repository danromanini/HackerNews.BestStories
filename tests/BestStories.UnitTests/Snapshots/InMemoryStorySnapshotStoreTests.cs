using BestStories.Core.Stories;
using BestStories.Infrastructure.Snapshots;
using static BestStories.UnitTests.TestData;

namespace BestStories.UnitTests.Snapshots;

public class InMemoryStorySnapshotStoreTests
{
    [Test]
    public void Current_StartsEmpty()
    {
        new InMemoryStorySnapshotStore().Current.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public async Task WaitForFirstSnapshotAsync_CompletesWhenTheFirstSnapshotIsPublished()
    {
        var store = new InMemoryStorySnapshotStore();
        var waiting = store.WaitForFirstSnapshotAsync(CancellationToken.None);
        waiting.IsCompleted.ShouldBeFalse();

        var snapshot = StorySnapshot.Create([Story(1, 10)], version: 1, Now);
        store.Publish(snapshot);

        (await waiting).ShouldBeSameAs(snapshot);
    }

    [Test]
    public async Task Publish_ReplacesTheCurrentSnapshotAtomically()
    {
        var store = new InMemoryStorySnapshotStore();
        var snapshots = Enumerable.Range(1, 500)
            .Select(version => StorySnapshot.Create([Story(version, version)], version, Now))
            .ToArray();

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                var current = store.Current;
                if (!current.IsEmpty)
                {
                    current.Stories.Single().Id.ShouldBe(current.Version);
                }
            }
        }));
        var writer = Task.Run(() => Array.ForEach(snapshots, store.Publish));

        await Task.WhenAll([.. readers, writer]);

        store.Current.Version.ShouldBe(500);
    }

    [Test]
    public async Task WaitForFirstSnapshotAsync_HonoursCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var waiting = new InMemoryStorySnapshotStore().WaitForFirstSnapshotAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => waiting);
    }
}
