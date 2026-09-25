namespace BestStories.Core.Stories;

public sealed record Story(
    long Id,
    string Title,
    string? Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount);
