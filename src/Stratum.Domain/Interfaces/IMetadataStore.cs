namespace Stratum.Domain.Interfaces;

using Stratum.Domain.Entities;

/// <summary>
/// Defines the contract for storing and retrieving S3 metadata.
/// Metadata includes bucket information, object metadata, and multipart upload state.
/// Implementations can use various backends such as SQLite, RocksDB, or distributed stores.
/// </summary>
public interface IMetadataStore
{
    // Bucket operations

    /// <summary>
    /// Creates a new bucket in the metadata store.
    /// </summary>
    /// <param name="bucket">The bucket to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateBucketAsync(Bucket bucket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bucket from the metadata store.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a bucket by name.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The bucket if found; otherwise, null.</returns>
    Task<Bucket?> GetBucketAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all buckets in the metadata store.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of all buckets.</returns>
    Task<IReadOnlyList<Bucket>> ListBucketsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bucket exists.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the bucket exists; otherwise, false.</returns>
    Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the statistics for a bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket to update.</param>
    /// <param name="objectCountDelta">The change in object count.</param>
    /// <param name="sizeDelta">The change in total size in bytes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateBucketStatisticsAsync(string bucketName, long objectCountDelta, long sizeDelta, CancellationToken cancellationToken = default);

    // Object operations

    /// <summary>
    /// Stores metadata for an object.
    /// </summary>
    /// <param name="metadata">The object metadata to store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PutObjectMetadataAsync(ObjectMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata for an object.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The object metadata if found; otherwise, null.</returns>
    Task<ObjectMetadata?> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes metadata for an object.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes metadata for multiple objects.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="keys">The object keys to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteMultipleObjectMetadataAsync(string bucketName, IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an object exists.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the object exists; otherwise, false.</returns>
    Task<bool> ObjectExistsAsync(string bucketName, string key, CancellationToken cancellationToken = default);

    // List operations (performance-critical)

    /// <summary>
    /// Lists objects in a bucket with optional filtering.
    /// This is an async enumerable for efficient streaming of large result sets.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="prefix">Optional prefix to filter objects.</param>
    /// <param name="delimiter">Optional delimiter for simulating directories.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An async enumerable of object metadata.</returns>
    IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        string? delimiter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists objects in a bucket with pagination support.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="prefix">Optional prefix to filter objects.</param>
    /// <param name="delimiter">Optional delimiter for simulating directories.</param>
    /// <param name="continuationToken">Optional continuation token for pagination.</param>
    /// <param name="maxKeys">Maximum number of keys to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing the list of objects, common prefixes, and the next continuation token.</returns>
    Task<(IReadOnlyList<ObjectMetadata> Objects, IReadOnlyList<string> CommonPrefixes, string? NextContinuationToken)> ListObjectsV2Async(
        string bucketName,
        string? prefix = null,
        string? delimiter = null,
        string? continuationToken = null,
        int maxKeys = 1000,
        CancellationToken cancellationToken = default);

    // Multipart upload operations

    /// <summary>
    /// Creates a new multipart upload record.
    /// </summary>
    /// <param name="upload">The multipart upload to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateMultipartUploadAsync(MultipartUpload upload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a multipart upload by upload ID.
    /// </summary>
    /// <param name="uploadId">The upload ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The multipart upload if found; otherwise, null.</returns>
    Task<MultipartUpload?> GetMultipartUploadAsync(string uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all multipart uploads for a bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="prefix">Optional prefix to filter uploads.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of multipart uploads.</returns>
    Task<IReadOnlyList<MultipartUpload>> ListMultipartUploadsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a part to a multipart upload.
    /// </summary>
    /// <param name="uploadId">The upload ID.</param>
    /// <param name="part">The part to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddPartAsync(string uploadId, MultipartPart part, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a multipart upload.
    /// </summary>
    /// <param name="uploadId">The upload ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CompleteMultipartUploadAsync(string uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts a multipart upload.
    /// </summary>
    /// <param name="uploadId">The upload ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AbortMultipartUploadAsync(string uploadId, CancellationToken cancellationToken = default);

    // Access key operations

    /// <summary>
    /// Creates a new access key.
    /// </summary>
    /// <param name="accessKey">The access key to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateAccessKeyAsync(AccessKey accessKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an access key by ID.
    /// </summary>
    /// <param name="accessKeyId">The access key ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The access key if found; otherwise, null.</returns>
    Task<AccessKey?> GetAccessKeyAsync(string accessKeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all access keys.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of all access keys.</returns>
    Task<IReadOnlyList<AccessKey>> ListAccessKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an access key.
    /// </summary>
    /// <param name="accessKeyId">The access key ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAccessKeyAsync(string accessKeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an access key.
    /// </summary>
    /// <param name="accessKey">The access key to update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAccessKeyAsync(AccessKey accessKey, CancellationToken cancellationToken = default);
}
