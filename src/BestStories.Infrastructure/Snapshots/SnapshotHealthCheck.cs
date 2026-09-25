using BestStories.Core.Abstractions;
using BestStories.Core.Stories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BestStories.Infrastructure.Snapshots;

internal sealed class SnapshotHealthCheck(
    IStorySnapshotStore snapshotStore,
    IOptionsMonitor<RefreshOptions> options,
    TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = snapshotStore.Current;

        if (snapshot.IsEmpty)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The best stories snapshot has not been loaded yet."));
        }

        var age = timeProvider.GetUtcNow() - snapshot.RefreshedAt;
        var data = new Dictionary<string, object>
        {
            ["version"] = snapshot.Version,
            ["stories"] = snapshot.Stories.Length,
            ["refreshedAt"] = snapshot.RefreshedAt,
            ["ageSeconds"] = Math.Round(age.TotalSeconds),
        };

        var result = age > options.CurrentValue.StaleAfter
            ? HealthCheckResult.Degraded("The best stories snapshot is stale; serving last known data.", data: data)
            : HealthCheckResult.Healthy("The best stories snapshot is fresh.", data);

        return Task.FromResult(result);
    }
}
