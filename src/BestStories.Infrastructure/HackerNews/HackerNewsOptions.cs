using System.ComponentModel.DataAnnotations;

namespace BestStories.Infrastructure.HackerNews;

public sealed class HackerNewsOptions
{
    public const string SectionName = "BestStories:HackerNews";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://hacker-news.firebaseio.com/v0/");

    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(5);

    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;
}
