namespace Stratum.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.IO;

/// <summary>
/// Health check that verifies available disk space.
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly string _dataDirectory;
    private readonly long _minimumFreeSpaceBytes;

    public DiskSpaceHealthCheck(string dataDirectory, long minimumFreeSpaceBytes = 1024 * 1024 * 1024) // 1GB default
    {
        _dataDirectory = dataDirectory;
        _minimumFreeSpaceBytes = minimumFreeSpaceBytes;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var driveRoot = Path.GetPathRoot(_dataDirectory);
            if (string.IsNullOrEmpty(driveRoot))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Could not determine drive root from data directory"));
            }

            var driveInfo = new DriveInfo(driveRoot);
            var freeSpace = driveInfo.AvailableFreeSpace;

            var data = new Dictionary<string, object>
            {
                ["dataDirectory"] = _dataDirectory,
                ["freeSpaceBytes"] = freeSpace,
                ["freeSpaceMB"] = freeSpace / (1024 * 1024),
                ["minimumFreeSpaceBytes"] = _minimumFreeSpaceBytes,
                ["minimumFreeSpaceMB"] = _minimumFreeSpaceBytes / (1024 * 1024)
            };

            if (freeSpace < _minimumFreeSpaceBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Insufficient disk space",
                    data: data));
            }

            if (freeSpace < _minimumFreeSpaceBytes * 2)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "Disk space running low",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Sufficient disk space",
                data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Failed to check disk space: {ex.Message}"));
        }
    }
}
