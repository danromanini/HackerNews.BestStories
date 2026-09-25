using BestStories.Core.Stories;
using Microsoft.Extensions.Options;
using Moq;

namespace BestStories.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    public static Story Story(long id, int score, int commentCount = 0) =>
        new(id, $"Story {id}", $"https://example.com/{id}", $"user{id}", Now.AddMinutes(-id), score, commentCount);

    public static IOptionsMonitor<T> Monitor<T>(T value)
        where T : class =>
        Mock.Of<IOptionsMonitor<T>>(monitor => monitor.CurrentValue == value);
}
