namespace Stratum.Api.Metrics;

using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Collects and tracks performance metrics for API operations.
/// Provides structured metrics collection for monitoring and analysis.
/// </summary>
public sealed class PerformanceMetrics
{
    private readonly ConcurrentDictionary<string, MetricData> _metrics = new();
    private readonly Stopwatch _globalStopwatch = Stopwatch.StartNew();

    private class MetricData
    {
        public long Count;
        public long TotalDurationMs;
        public long MinDurationMs = long.MaxValue;
        public long MaxDurationMs;
        public long ErrorCount;
        public DateTime LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="durationMs">The duration in milliseconds.</param>
    public void RecordSuccess(string operation, long durationMs)
    {
        var data = _metrics.AddOrUpdate(operation, 
            _ => new MetricData { Count = 1, TotalDurationMs = durationMs, MinDurationMs = durationMs, MaxDurationMs = durationMs },
            (_, existing) =>
            {
                existing.Count++;
                existing.TotalDurationMs += durationMs;
                existing.MinDurationMs = Math.Min(existing.MinDurationMs, durationMs);
                existing.MaxDurationMs = Math.Max(existing.MaxDurationMs, durationMs);
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="durationMs">The duration in milliseconds.</param>
    public void RecordError(string operation, long durationMs)
    {
        var data = _metrics.AddOrUpdate(operation,
            _ => new MetricData { Count = 1, TotalDurationMs = durationMs, MinDurationMs = durationMs, MaxDurationMs = durationMs, ErrorCount = 1 },
            (_, existing) =>
            {
                existing.Count++;
                existing.TotalDurationMs += durationMs;
                existing.MinDurationMs = Math.Min(existing.MinDurationMs, durationMs);
                existing.MaxDurationMs = Math.Max(existing.MaxDurationMs, durationMs);
                existing.ErrorCount++;
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });
    }

    /// <summary>
    /// Gets metrics for a specific operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <returns>The operation metrics or null if not found.</returns>
    public OperationMetrics? GetMetrics(string operation)
    {
        if (!_metrics.TryGetValue(operation, out var data))
        {
            return null;
        }

        return new OperationMetrics
        {
            Operation = operation,
            Count = data.Count,
            TotalDurationMs = data.TotalDurationMs,
            AverageDurationMs = data.Count > 0 ? data.TotalDurationMs / data.Count : 0,
            MinDurationMs = data.MinDurationMs,
            MaxDurationMs = data.MaxDurationMs,
            ErrorCount = data.ErrorCount,
            ErrorRate = data.Count > 0 ? (double)data.ErrorCount / data.Count : 0,
            LastUpdated = data.LastUpdated
        };
    }

    /// <summary>
    /// Gets all metrics.
    /// </summary>
    /// <returns>All collected metrics.</returns>
    public IEnumerable<OperationMetrics> GetAllMetrics()
    {
        return _metrics.Select(kvp => new OperationMetrics
        {
            Operation = kvp.Key,
            Count = kvp.Value.Count,
            TotalDurationMs = kvp.Value.TotalDurationMs,
            AverageDurationMs = kvp.Value.Count > 0 ? kvp.Value.TotalDurationMs / kvp.Value.Count : 0,
            MinDurationMs = kvp.Value.MinDurationMs,
            MaxDurationMs = kvp.Value.MaxDurationMs,
            ErrorCount = kvp.Value.ErrorCount,
            ErrorRate = kvp.Value.Count > 0 ? (double)kvp.Value.ErrorCount / kvp.Value.Count : 0,
            LastUpdated = kvp.Value.LastUpdated
        });
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        _metrics.Clear();
    }

    /// <summary>
    /// Gets the global uptime.
    /// </summary>
    /// <returns>The uptime duration.</returns>
    public TimeSpan GetUptime()
    {
        return _globalStopwatch.Elapsed;
    }
}

/// <summary>
/// Represents metrics for a specific operation.
/// </summary>
public sealed class OperationMetrics
{
    public string Operation { get; set; } = string.Empty;
    public long Count { get; set; }
    public long TotalDurationMs { get; set; }
    public long AverageDurationMs { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public long ErrorCount { get; set; }
    public double ErrorRate { get; set; }
    public DateTime LastUpdated { get; set; }
}
