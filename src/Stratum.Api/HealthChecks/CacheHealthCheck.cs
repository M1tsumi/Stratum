namespace Stratum.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Stratum.Domain.Interfaces;
using Stratum.Infrastructure.Storage.FileSystem;

/// <summary>
/// Health check for object store cache statistics.
/// Provides visibility into cache utilization and performance.
/// </summary>
public sealed class CacheHealthCheck : IHealthCheck
{
    private readonly CachedObjectStore? _cachedStore;

    public CacheHealthCheck(IObjectStore objectStore)
    {
        _cachedStore = objectStore as CachedObjectStore;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_cachedStore == null)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("Cache not enabled (using direct file system store)"));
        }

        var (count, bytes, _) = _cachedStore.GetCacheStats();
        var cachedMB = bytes / (1024.0 * 1024.0);
        var data = new Dictionary<string, object>
        {
            { "CachedItems", count },
            { "CachedBytes", bytes },
            { "CachedMB", cachedMB.ToString("F2") },
            { "CacheEnabled", true }
        };

        var status = count > 0 ? HealthStatus.Healthy : HealthStatus.Healthy;
        var description = count > 0
            ? $"Cache active: {count} items, {cachedMB:F2} MB"
            : "Cache active but empty";

        return Task.FromResult(
            HealthCheckResult.Healthy(description, data: data));
    }
}
