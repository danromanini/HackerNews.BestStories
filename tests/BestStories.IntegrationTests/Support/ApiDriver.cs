namespace BestStories.IntegrationTests.Support;

public sealed class ApiDriver : IDisposable
{
    private readonly Dictionary<string, string?> _settings = [];
    private BestStoriesApiFactory? _factory;
    private HttpClient? _client;

    public FakeHackerNewsApi HackerNews { get; } = new();

    public HttpClient Client => _client ??= Start().CreateClient();

    public void Configure(string key, string value)
    {
        if (_factory is not null)
        {
            throw new InvalidOperationException("Settings must be configured before the API is started.");
        }

        _settings[key] = value;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
        HackerNews.Dispose();
    }

    private BestStoriesApiFactory Start() => _factory = new BestStoriesApiFactory(HackerNews, _settings);
}
