using BestStories.Api.Stories;
using BestStories.Core.Abstractions;
using BestStories.Infrastructure.HackerNews;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BestStories.IntegrationTests.Support;

public sealed class BestStoriesApiFactory(FakeHackerNewsApi hackerNews, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    public static IReadOnlyDictionary<string, string?> DefaultSettings { get; } = new Dictionary<string, string?>
    {
        ["Logging:LogLevel:Default"] = "Warning",
        ["BestStories:Refresh:Interval"] = "01:00:00",
        ["BestStories:RateLimiting:PermitLimit"] = "100000",
    };

    public FakeHackerNewsApi HackerNews { get; } = hackerNews;

    public HubConnection CreateHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, BestStoriesHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in DefaultSettings.Concat(settings ?? new Dictionary<string, string?>()))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
            services.AddHttpClient<IHackerNewsClient, HackerNewsClient>()
                .ConfigurePrimaryHttpMessageHandler(() => HackerNews));
    }
}
