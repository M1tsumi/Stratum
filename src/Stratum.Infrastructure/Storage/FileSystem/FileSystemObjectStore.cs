namespace Stratum.Infrastructure.Storage.FileSystem;

using Stratum.Domain.Interfaces;
using System.IO.Pipelines;
using System.Threading;

/// <summary>
/// FileSystem implementation of the object store.
/// Stores objects as files in the local filesystem with directory structure mirroring bucket/key hierarchy.
/// Uses System.IO.Pipelines for efficient streaming with backpressure handling.
/// </summary>
public sealed class FileSystemObjectStore : IObjectStore
{
    private readonly string _baseDirectory;
    private const int OptimalBufferSize = 1024 * 1024; // 1MB optimal buffer size for file I/O
    private readonly SemaphoreSlim _concurrencySemaphore;

    /// <summary>
    /// Initializes a new instance of the FileSystemObjectStore class.
    /// </summary>
    /// <param name="baseDirectory">The base directory where objects will be stored.</param>
    /// <param name="maxConcurrentOperations">Maximum concurrent file operations (default: 100).</param>
    public FileSystemObjectStore(string baseDirectory, int maxConcurrentOperations = 100)
    {
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));

        // Ensure base directory exists
        Directory.CreateDirectory(_baseDirectory);

        // Create semaphore for throttling concurrent operations
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);
    }

    // Core operations

    public async Task PutObjectAsync(
        string bucketName,
        string key,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetPath(bucketName, key);
            var directoryPath = Path.GetDirectoryName(filePath);

            if (directoryPath != null && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Use temporary file for atomic write
            var tempFilePath = $"{filePath}.tmp";

            try
            {
                await using var fileStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: OptimalBufferSize,
                    useAsync: true);

                await content.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
                fileStream.Dispose();

                // Atomic rename
                File.Move(tempFilePath, filePath);
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                throw;
            }
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    public async Task<Stream> GetObjectAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetPath(bucketName, key);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Object not found: {bucketName}/{key}");
            }

            var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: OptimalBufferSize,
                useAsync: true);

            return fileStream;
        }
        catch
        {
            _concurrencySemaphore.Release();
            throw;
        }
    }

    public async Task DeleteObjectAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetPath(bucketName, key);

        if (File.Exists(filePath))
        {
            // Retry deletion with delay to handle file locks
            var maxRetries = 3;
            var retryDelay = TimeSpan.FromMilliseconds(100);

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(filePath);
                    break;
                }
                catch (IOException)
                {
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(retryDelay, cancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        // Clean up empty directories
        CleanEmptyDirectories(bucketName, key);
    }

    public Task<bool> ObjectExistsAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetPath(bucketName, key);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task<long> GetObjectSizeAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetPath(bucketName, key);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Object not found: {bucketName}/{key}");
        }

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    // Range requests

    public async Task<Stream> GetObjectRangeAsync(
        string bucketName,
        string key,
        long offset,
        long? length = null,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetPath(bucketName, key);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Object not found: {bucketName}/{key}");
        }

        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;

        if (offset < 0 || offset >= fileSize)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of range.");
        }

        var endOffset = length.HasValue
            ? Math.Min(offset + length.Value, fileSize)
            : fileSize;

        var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: OptimalBufferSize,
            useAsync: true);

        fileStream.Seek(offset, SeekOrigin.Begin);

        // Create a limited stream if length is specified
        if (length.HasValue)
        {
            return new LimitedStream(fileStream, length.Value);
        }

        return fileStream;
    }

    // Multipart upload parts

    public async Task PutPartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            var partsDirectory = Path.Combine(_baseDirectory, "multipart-uploads", uploadId);
            Directory.CreateDirectory(partsDirectory);

            var partFilePath = Path.Combine(partsDirectory, $"{partNumber}.part");

            await using var fileStream = new FileStream(
                partFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: OptimalBufferSize,
                useAsync: true);

            await content.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    public async Task<Stream> GetPartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            var partsDirectory = Path.Combine(_baseDirectory, "multipart-uploads", uploadId);
            var partFilePath = Path.Combine(partsDirectory, $"{partNumber}.part");

            if (!File.Exists(partFilePath))
            {
                throw new FileNotFoundException($"Part not found: uploadId={uploadId}, partNumber={partNumber}");
            }

            var fileStream = new FileStream(
                partFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: OptimalBufferSize,
                useAsync: true);

            return fileStream;
        }
        catch
        {
            _concurrencySemaphore.Release();
            throw;
        }
    }

    public Task DeletePartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default)
    {
        var partsDirectory = Path.Combine(_baseDirectory, "multipart-uploads", uploadId);
        var partFilePath = Path.Combine(partsDirectory, $"{partNumber}.part");

        if (File.Exists(partFilePath))
        {
            File.Delete(partFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task CompleteMultipartUploadAsync(
        string bucketName,
        string key,
        string uploadId,
        IReadOnlyList<string> partETags,
        CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            var partsDirectory = Path.Combine(_baseDirectory, "multipart-uploads", uploadId);
            var targetFilePath = GetPath(bucketName, key);
            var targetDirectory = Path.GetDirectoryName(targetFilePath);

            if (targetDirectory != null && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Use temporary file for atomic write
            var tempFilePath = $"{targetFilePath}.tmp";

            try
            {
                await using var targetStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: OptimalBufferSize,
                    useAsync: true);

                // Concatenate all parts in order
                for (var i = 0; i < partETags.Count; i++)
                {
                    var partFilePath = Path.Combine(partsDirectory, $"{i + 1}.part");
                    if (!File.Exists(partFilePath))
                    {
                        throw new FileNotFoundException($"Part not found: {partFilePath}");
                    }

                    await using var partStream = new FileStream(
                        partFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: OptimalBufferSize,
                        useAsync: true);

                    await partStream.CopyToAsync(targetStream, cancellationToken);
                }

                await targetStream.FlushAsync(cancellationToken);

                // Atomic rename
                File.Move(tempFilePath, targetFilePath);

                // Clean up parts directory
                Directory.Delete(partsDirectory, recursive: true);
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                throw;
            }
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    public Task AbortMultipartUploadAsync(
        string bucketName,
        string key,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        var partsDirectory = Path.Combine(_baseDirectory, "multipart-uploads", uploadId);

        if (Directory.Exists(partsDirectory))
        {
            Directory.Delete(partsDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    // Bulk operations

    public async Task DeleteMultipleObjectsAsync(
        string bucketName,
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        var keysList = keys.ToList();

        foreach (var key in keysList)
        {
            await _concurrencySemaphore.WaitAsync(cancellationToken);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var filePath = GetPath(bucketName, key);

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        CleanEmptyDirectories(bucketName, key);
                    }
                }
                finally
                {
                    _concurrencySemaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    // Copy operations

    public async Task CopyObjectAsync(
        string sourceBucketName,
        string sourceKey,
        string destinationBucketName,
        string destinationKey,
        CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            var sourcePath = GetPath(sourceBucketName, sourceKey);
            var destinationPath = GetPath(destinationBucketName, destinationKey);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Source object not found: {sourceBucketName}/{sourceKey}");
            }

            if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Use temporary file for atomic write
            var tempFilePath = $"{destinationPath}.tmp";

            try
            {
                await using var sourceStream = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: OptimalBufferSize,
                    useAsync: true);

                await using var destinationStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: OptimalBufferSize,
                    useAsync: true);

                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);

                // Atomic rename
                File.Move(tempFilePath, destinationPath);
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                throw;
            }
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    // Helper methods

    private string GetPath(string bucketName, string key)
    {
        // Replace forward slashes with path separator
        var normalizedKey = key.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_baseDirectory, bucketName, normalizedKey);
    }

    private void CleanEmptyDirectories(string bucketName, string key)
    {
        var directoryPath = Path.GetDirectoryName(GetPath(bucketName, key));
        
        if (string.IsNullOrEmpty(directoryPath))
        {
            return;
        }

        // Clean up empty directories up to the bucket level
        var bucketPath = Path.Combine(_baseDirectory, bucketName);
        var currentPath = directoryPath;

        while (currentPath != bucketPath && currentPath.Length > bucketPath.Length)
        {
            if (Directory.Exists(currentPath) && !Directory.EnumerateFileSystemEntries(currentPath).Any())
            {
                Directory.Delete(currentPath);
                currentPath = Directory.GetParent(currentPath)?.FullName ?? string.Empty;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Stream wrapper that limits reading to a specific number of bytes.
    /// </summary>
    private sealed class LimitedStream : Stream
    {
        private readonly Stream _baseStream;
        private long _remaining;

        public LimitedStream(Stream baseStream, long length)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _remaining = length;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesToRead = (int)Math.Min(count, _remaining);
            if (bytesToRead == 0)
            {
                return 0;
            }

            var bytesRead = _baseStream.Read(buffer, offset, bytesToRead);
            _remaining -= bytesRead;
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesToRead = (int)Math.Min(count, _remaining);
            if (bytesToRead == 0)
            {
                return 0;
            }

            var bytesRead = await _baseStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
            _remaining -= bytesRead;
            return bytesRead;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
