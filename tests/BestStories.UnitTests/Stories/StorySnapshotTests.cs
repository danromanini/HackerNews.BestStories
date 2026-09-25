using BestStories.Core.Stories;
using static BestStories.UnitTests.TestData;

namespace BestStories.UnitTests.Stories;

public class StorySnapshotTests
{
    [Test]
    public void Create_OrdersStoriesByScoreDescending()
    {
        var snapshot = StorySnapshot.Create([Story(1, 10), Story(2, 300), Story(3, 50)], version: 1, Now);

        snapshot.Stories.Select(story => story.Id).ShouldBe([2, 3, 1]);
    }

    [Test]
    public void Create_BreaksScoreTiesByIdSoOrderingIsDeterministic()
    {
        var snapshot = StorySnapshot.Create([Story(9, 100), Story(4, 100), Story(7, 100)], version: 1, Now);

        snapshot.Stories.Select(story => story.Id).ShouldBe([4, 7, 9]);
    }

    [TestCase(0, 0)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(50, 3)]
    public void Top_ReturnsAtMostTheAvailableStories(int requested, int expected)
    {
        var snapshot = StorySnapshot.Create([Story(1, 10), Story(2, 20), Story(3, 30)], version: 1, Now);

        var top = snapshot.Top(requested);

        top.Length.ShouldBe(expected);
        top.Select(story => story.Score).ShouldBeInOrder(SortDirection.Descending);
    }

    [Test]
    public void Top_RejectsNegativeCounts()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => StorySnapshot.Empty.Top(-1));
    }

    [Test]
    public void Empty_IsFlaggedAsEmpty()
    {
        StorySnapshot.Empty.IsEmpty.ShouldBeTrue();
        StorySnapshot.Create([], version: 1, Now).IsEmpty.ShouldBeFalse();
    }

    [Test]
    public void HasSameStoriesAs_ComparesStoryValuesIgnoringVersionAndTime()
    {
        var first = StorySnapshot.Create([Story(1, 10), Story(2, 20)], version: 1, Now);
        var sameContent = StorySnapshot.Create([Story(2, 20), Story(1, 10)], version: 2, Now.AddMinutes(1));
        var scoreChanged = StorySnapshot.Create([Story(1, 11), Story(2, 20)], version: 2, Now);

        first.HasSameStoriesAs(sameContent).ShouldBeTrue();
        first.HasSameStoriesAs(scoreChanged).ShouldBeFalse();
    }

    [Test]
    public void ContentHash_DependsOnlyOnTheStories()
    {
        var first = StorySnapshot.Create([Story(1, 10), Story(2, 20)], version: 1, Now);
        var sameContentLater = StorySnapshot.Create([Story(2, 20), Story(1, 10)], version: 9, Now.AddHours(1));
        var commentsChanged = StorySnapshot.Create([Story(1, 10, commentCount: 5), Story(2, 20)], version: 1, Now);

        first.ContentHash.ShouldBe(sameContentLater.ContentHash);
        first.ContentHash.ShouldNotBe(commentsChanged.ContentHash);
        (first with { RefreshedAt = Now.AddDays(1) }).ContentHash.ShouldBe(first.ContentHash);
    }
}
