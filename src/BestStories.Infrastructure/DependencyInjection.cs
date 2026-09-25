using System.Net.Http.Headers;
using BestStories.Core.Abstractions;
using BestStories.Core.Stories;
using BestStories.Infrastructure.HackerNews;
using BestStories.Infrastructure.Snapshots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace BestStories.Infrastructure;

public static class DependencyInjection
{
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddBestStories(this IServiceCollection services)
    {
        services.AddOptions<RefreshOptions>()
            .BindConfiguration(RefreshOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<HackerNewsOptions>()
            .BindConfiguration(HackerNewsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IStorySnapshotStore, InMemoryStorySnapshotStore>();
        services.AddSingleton<IBestStoriesService, BestStoriesService>();
        services.AddSingleton<BestStoriesRefresher>();
        services.AddHostedService<BestStoriesRefreshWorker>();

        services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(ConfigureHttpClient)
            .AddStandardResilienceHandler()
            .Configure(ConfigureResilience);

        services.AddHealthChecks()
            .AddCheck<SnapshotHealthCheck>("best-stories-snapshot", tags: [ReadinessTag]);

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;

        client.BaseAddress = options.BaseAddress;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BestStoriesApi", "1.0"));
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions resilience, IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;

        resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
        resilience.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
        resilience.Retry.UseJitter = true;
        resilience.TotalRequestTimeout.Timeout = options.AttemptTimeout * (options.MaxRetryAttempts + 1) + TimeSpan.FromSeconds(10);
        resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(30).Ticks, options.AttemptTimeout.Ticks * 2));
    }
}
