namespace Stratum.Infrastructure.Storage.FileSystem;

using Stratum.Domain.Interfaces;
using System.Collections.Concurrent;

/// <summary>
/// Cached wrapper for object store that provides in-memory caching for frequently accessed files.
/// Uses LRU eviction policy with configurable size limits.
/// </summary>
public sealed class CachedObjectStore : IObjectStore
{
    private readonly IObjectStore _innerStore;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly int _maxCacheSize;
    private readonly long _maxCacheBytes;
    private long _currentCacheBytes;
    private readonly object _lock = new();

    private class CacheEntry
    {
        public byte[] Data;
        public DateTime LastAccessed;
        public long Size;
    }

    /// <summary>
    /// Initializes a new instance of the CachedObjectStore class.
    /// </summary>
    /// <param name="innerStore">The underlying object store to wrap.</param>
    /// <param name="maxCacheSize">Maximum number of items to cache (default: 1000).</param>
    /// <param name="maxCacheBytes">Maximum total bytes to cache (default: 1GB).</param>
    public CachedObjectStore(IObjectStore innerStore, int maxCacheSize = 1000, long maxCacheBytes = 1024 * 1024 * 1024)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
        _maxCacheSize = maxCacheSize;
        _maxCacheBytes = maxCacheBytes;
        _cache = new ConcurrentDictionary<string, CacheEntry>();
    }

    // Core operations

    public async Task PutObjectAsync(
        string bucketName,
        string key,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(bucketName, key);

        // Read content into memory for caching
        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);
        var data = memoryStream.ToArray();

        // Store in inner store
        using var contentStream = new MemoryStream(data);
        await _innerStore.PutObjectAsync(bucketName, key, contentStream, cancellationToken);

        // Update cache
        lock (_lock)
        {
            EnsureCapacity(data.Length);
            _cache[cacheKey] = new CacheEntry
            {
                Data = data,
                LastAccessed = DateTime.UtcNow,
                Size = data.Length
            };
            _currentCacheBytes += data.Length;
        }
    }

    public async Task<Stream> GetObjectAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(bucketName, key);

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            lock (_lock)
            {
                entry.LastAccessed = DateTime.UtcNow;
            }
            return new MemoryStream(entry.Data, writable: false);
        }

        // Not in cache, fetch from inner store
        var stream = await _innerStore.GetObjectAsync(bucketName, key, cancellationToken);

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        var data = memoryStream.ToArray();

        // Add to cache
        lock (_lock)
        {
            EnsureCapacity(data.Length);
            _cache[cacheKey] = new CacheEntry
            {
                Data = data,
                LastAccessed = DateTime.UtcNow,
                Size = data.Length
            };
            _currentCacheBytes += data.Length;
        }

        return new MemoryStream(data, writable: false);
    }

    public Task DeleteObjectAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(bucketName, key);

        // Remove from cache
        if (_cache.TryRemove(cacheKey, out var entry))
        {
            lock (_lock)
            {
                _currentCacheBytes -= entry.Size;
            }
        }

        // Delete from inner store
        return _innerStore.DeleteObjectAsync(bucketName, key, cancellationToken);
    }

    public Task<bool> ObjectExistsAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        return _innerStore.ObjectExistsAsync(bucketName, key, cancellationToken);
    }

    public Task<long> GetObjectSizeAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        return _innerStore.GetObjectSizeAsync(bucketName, key, cancellationToken);
    }

    // Range requests

    public Task<Stream> GetObjectRangeAsync(
        string bucketName,
        string key,
        long offset,
        long? length = null,
        CancellationToken cancellationToken = default)
    {
        // Range requests bypass cache
        return _innerStore.GetObjectRangeAsync(bucketName, key, offset, length, cancellationToken);
    }

    // Multipart upload parts

    public Task PutPartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        // Multipart parts bypass cache
        return _innerStore.PutPartAsync(bucketName, key, uploadId, partNumber, content, cancellationToken);
    }

    public Task<Stream> GetPartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default)
    {
        // Multipart parts bypass cache
        return _innerStore.GetPartAsync(bucketName, key, uploadId, partNumber, cancellationToken);
    }

    public Task DeletePartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default)
    {
        return _innerStore.DeletePartAsync(bucketName, key, uploadId, partNumber, cancellationToken);
    }

    public Task CompleteMultipartUploadAsync(
        string bucketName,
        string key,
        string uploadId,
        IReadOnlyList<string> partETags,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(bucketName, key);

        // Remove from cache if exists (object being replaced)
        if (_cache.TryRemove(cacheKey, out var entry))
        {
            lock (_lock)
            {
                _currentCacheBytes -= entry.Size;
            }
        }

        return _innerStore.CompleteMultipartUploadAsync(bucketName, key, uploadId, partETags, cancellationToken);
    }

    public Task AbortMultipartUploadAsync(
        string bucketName,
        string key,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        return _innerStore.AbortMultipartUploadAsync(bucketName, key, uploadId, cancellationToken);
    }

    // Bulk operations

    public Task DeleteMultipleObjectsAsync(
        string bucketName,
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var cacheKey = GetCacheKey(bucketName, key);

            // Remove from cache
            if (_cache.TryRemove(cacheKey, out var entry))
            {
                lock (_lock)
                {
                    _currentCacheBytes -= entry.Size;
                }
            }
        }

        return _innerStore.DeleteMultipleObjectsAsync(bucketName, keys, cancellationToken);
    }

    // Copy operations

    public async Task CopyObjectAsync(
        string sourceBucketName,
        string sourceKey,
        string destinationBucketName,
        string destinationKey,
        CancellationToken cancellationToken = default)
    {
        var sourceCacheKey = GetCacheKey(sourceBucketName, sourceKey);
        var destCacheKey = GetCacheKey(destinationBucketName, destinationKey);

        byte[]? data = null;

        // Try to get from cache
        if (_cache.TryGetValue(sourceCacheKey, out var entry))
        {
            data = entry.Data;
            lock (_lock)
            {
                entry.LastAccessed = DateTime.UtcNow;
            }
        }
        else
        {
            // Fetch from inner store
            var stream = await _innerStore.GetObjectAsync(sourceBucketName, sourceKey, cancellationToken);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            data = memoryStream.ToArray();
        }

        // Put in destination
        using var contentStream = new MemoryStream(data);
        await _innerStore.PutObjectAsync(destinationBucketName, destinationKey, contentStream, cancellationToken);

        // Add to cache
        lock (_lock)
        {
            EnsureCapacity(data.Length);
            _cache[destCacheKey] = new CacheEntry
            {
                Data = data,
                LastAccessed = DateTime.UtcNow,
                Size = data.Length
            };
            _currentCacheBytes += data.Length;
        }
    }

    // Helper methods

    private static string GetCacheKey(string bucketName, string key)
    {
        return $"{bucketName}/{key}";
    }

    private void EnsureCapacity(long requiredBytes)
    {
        // Evict least recently used entries if needed
        while (_cache.Count >= _maxCacheSize || _currentCacheBytes + requiredBytes > _maxCacheBytes)
        {
            var lruKey = _cache.OrderBy(kvp => kvp.Value.LastAccessed).FirstOrDefault().Key;
            if (lruKey == null) break;

            if (_cache.TryRemove(lruKey, out var removedEntry))
            {
                _currentCacheBytes -= removedEntry.Size;
            }
        }
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void ClearCache()
    {
        lock (_lock)
        {
            _cache.Clear();
            _currentCacheBytes = 0;
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public (int Count, long Bytes, double HitRate) GetCacheStats()
    {
        lock (_lock)
        {
            return (_cache.Count, _currentCacheBytes, 0.0); // Hit rate would need tracking
        }
    }
}
