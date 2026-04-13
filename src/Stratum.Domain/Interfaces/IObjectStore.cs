namespace Stratum.Domain.Interfaces;

/// <summary>
/// Defines the contract for storing and retrieving object data.
/// Implementations can use various backends such as filesystem, cloud storage, or distributed systems.
/// </summary>
public interface IObjectStore
{
    // Core operations

    /// <summary>
    /// Stores an object in the specified bucket with the given key.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="content">The content stream to store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PutObjectAsync(
        string bucketName,
        string key,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an object from the specified bucket with the given key.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A stream containing the object content.</returns>
    Task<Stream> GetObjectAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from the specified bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteObjectAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an object exists in the specified bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the object exists; otherwise, false.</returns>
    Task<bool> ObjectExistsAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the size of an object in bytes.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The size of the object in bytes.</returns>
    Task<long> GetObjectSizeAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    // Range requests (for resumable downloads)

    /// <summary>
    /// Retrieves a byte range from an object.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="offset">The starting byte offset.</param>
    /// <param name="length">The number of bytes to retrieve, or null for the remainder of the object.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A stream containing the requested byte range.</returns>
    Task<Stream> GetObjectRangeAsync(
        string bucketName,
        string key,
        long offset,
        long? length = null,
        CancellationToken cancellationToken = default);

    // Multipart upload parts

    /// <summary>
    /// Stores a part of a multipart upload.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="uploadId">The multipart upload ID.</param>
    /// <param name="partNumber">The part number (1-10000).</param>
    /// <param name="content">The content stream for this part.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PutPartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a part of a multipart upload.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="uploadId">The multipart upload ID.</param>
    /// <param name="partNumber">The part number (1-10000).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A stream containing the part content.</returns>
    Task<Stream> GetPartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a part of a multipart upload.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="uploadId">The multipart upload ID.</param>
    /// <param name="partNumber">The part number (1-10000).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeletePartAsync(
        string bucketName,
        string key,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a multipart upload by assembling all parts into the final object.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="uploadId">The multipart upload ID.</param>
    /// <param name="partETags">The ETags of all parts in order.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CompleteMultipartUploadAsync(
        string bucketName,
        string key,
        string uploadId,
        IReadOnlyList<string> partETags,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts a multipart upload and cleans up all associated parts.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="uploadId">The multipart upload ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AbortMultipartUploadAsync(
        string bucketName,
        string key,
        string uploadId,
        CancellationToken cancellationToken = default);

    // Bulk operations

    /// <summary>
    /// Deletes multiple objects from the specified bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="keys">The object keys to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteMultipleObjectsAsync(
        string bucketName,
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    // Copy operations

    /// <summary>
    /// Copies an object within the same bucket or to a different bucket.
    /// </summary>
    /// <param name="sourceBucketName">The source bucket name.</param>
    /// <param name="sourceKey">The source object key.</param>
    /// <param name="destinationBucketName">The destination bucket name.</param>
    /// <param name="destinationKey">The destination object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CopyObjectAsync(
        string sourceBucketName,
        string sourceKey,
        string destinationBucketName,
        string destinationKey,
        CancellationToken cancellationToken = default);
}
