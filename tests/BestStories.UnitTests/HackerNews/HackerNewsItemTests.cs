using BestStories.Infrastructure.HackerNews;

namespace BestStories.UnitTests.HackerNews;

public class HackerNewsItemTests
{
    private static HackerNewsItem Item(
        string? type = "story",
        string? title = "A uBlock Origin update was rejected from the Chrome Web Store",
        string? url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
        bool deleted = false,
        bool dead = false) =>
        new(21233041, type, "ismaildonmez", 1570887781, title, url, 1716, 572, deleted, dead);

    [Test]
    public void ToStory_MapsTheHackerNewsFieldsToTheStoryContract()
    {
        var story = Item().ToStory();

        story.ShouldNotBeNull();
        story.ShouldSatisfyAllConditions(
            () => story.Id.ShouldBe(21233041),
            () => story.Title.ShouldBe("A uBlock Origin update was rejected from the Chrome Web Store"),
            () => story.Uri.ShouldBe("https://github.com/uBlockOrigin/uBlock-issues/issues/745"),
            () => story.PostedBy.ShouldBe("ismaildonmez"),
            () => story.Time.ShouldBe(new DateTimeOffset(2019, 10, 12, 13, 43, 1, TimeSpan.Zero)),
            () => story.Score.ShouldBe(1716),
            () => story.CommentCount.ShouldBe(572));
    }

    [Test]
    public void ToStory_KeepsStoriesWithoutUrl()
    {
        Item(url: null).ToStory()!.Uri.ShouldBeNull();
    }

    [TestCase("job")]
    [TestCase("comment")]
    [TestCase(null)]
    public void ToStory_IgnoresItemsThatAreNotStories(string? type)
    {
        Item(type: type).ToStory().ShouldBeNull();
    }

    [Test]
    public void ToStory_IgnoresDeletedDeadOrUntitledItems()
    {
        Item(deleted: true).ToStory().ShouldBeNull();
        Item(dead: true).ToStory().ShouldBeNull();
        Item(title: null).ToStory().ShouldBeNull();
    }
}
