namespace Stratum.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Stratum.Domain.Entities;
using Stratum.Domain.Interfaces;
using Stratum.Domain.Services;

/// <summary>
/// S3 object API endpoints.
/// </summary>
public static class ObjectEndpoints
{
    /// <summary>
    /// Registers object endpoints.
    /// </summary>
    public static void MapObjectEndpoints(this IEndpointRouteBuilder app)
    {
        // PutObject
        app.MapPut("/{bucket}/{*key}", PutObjectAsync)
            .WithName("PutObject");

        // GetObject
        app.MapGet("/{bucket}/{*key}", GetObjectAsync)
            .WithName("GetObject");

        // HeadObject
        app.MapMethods("/{bucket}/{*key}", new[] { "HEAD" }, HeadObjectAsync)
            .WithName("HeadObject");

        // DeleteObject
        app.MapDelete("/{bucket}/{*key}", DeleteObjectAsync)
            .WithName("DeleteObject");

        // DeleteMultipleObjects
        app.MapPost("/{bucket}/delete", DeleteMultipleObjectsAsync)
            .WithName("DeleteMultipleObjects");

        // ListObjectsV2
        app.MapGet("/{bucket}", ListObjectsV2Async)
            .WithName("ListObjectsV2");
    }

    /// <summary>
    /// Uploads an object.
    /// </summary>
    private static async Task<IResult> PutObjectAsync(
        string bucket,
        string? key,
        HttpRequest request,
        IMetadataStore metadataStore,
        IObjectStore objectStore,
        ETagCalculator eTagCalculator,
        CancellationToken cancellationToken)
    {
        var requestId = request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty. Provide a valid object key in the URL path.",
                Resource = $"{bucket}/",
                RequestId = requestId
            });
        }

        try
        {
            var bucketExists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
            if (!bucketExists)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchBucket",
                    Message = $"Bucket '{bucket}' does not exist. Create the bucket using PUT /{bucket} before uploading objects.",
                    Resource = bucket,
                    RequestId = requestId
                });
            }

            var contentType = request.ContentType ?? "application/octet-stream";

            // Buffer the request body to allow multiple reads (for ETag calculation and upload)
            using var bodyBuffer = new MemoryStream();
            await request.Body.CopyToAsync(bodyBuffer, cancellationToken);
            bodyBuffer.Position = 0; // Reset position to beginning

            var eTag = await eTagCalculator.CalculateSinglePartETagAsync(bodyBuffer, cancellationToken);
            bodyBuffer.Position = 0; // Reset position again for upload

            await objectStore.PutObjectAsync(bucket, key, bodyBuffer, cancellationToken);

            var metadata = new ObjectMetadata(
                bucket,
                key,
                $"\"{eTag}\"",
                request.ContentLength ?? bodyBuffer.Length,
                contentType,
                request.Headers.ContentEncoding,
                request.Headers.ContentDisposition,
                null,
                request.Headers.CacheControl,
                new Dictionary<string, string>(),
                "STANDARD",
                null,
                true,
                false,
                DateTime.UtcNow);

            await metadataStore.PutObjectMetadataAsync(metadata, cancellationToken);
            await metadataStore.UpdateBucketStatisticsAsync(bucket, 1, metadata.Size, cancellationToken);

            return Results.Ok(new
            {
                ETag = $"\"{eTag}\"",
                LastModified = DateTime.UtcNow.ToString("R")
            });
        }
        catch (IOException)
        {
            return Results.Problem(detail: $"I/O error uploading object '{key}' to bucket '{bucket}'. Check disk space and permissions.",
                statusCode: 500, title: "Storage Error", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ErrorType", "IOException" }
                });
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(detail: $"Upload operation for object '{key}' was cancelled.",
                statusCode: 499, title: "Operation Cancelled", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") }
                });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: $"Failed to upload object '{key}' to bucket '{bucket}': {ex.Message}. Request ID: {requestId}",
                statusCode: 500, title: "Upload Failed", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ErrorType", ex.GetType().Name }
                });
        }
    }

    /// <summary>
    /// Downloads an object.
    /// </summary>
    private static async Task<IResult> GetObjectAsync(
        string bucket,
        string? key,
        HttpRequest request,
        IMetadataStore metadataStore,
        IObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var requestId = request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty. Provide a valid object key in the URL path.",
                Resource = $"{bucket}/",
                RequestId = requestId
            });
        }

        try
        {
            var metadata = await metadataStore.GetObjectMetadataAsync(bucket, key, cancellationToken);
            if (metadata == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchKey",
                    Message = $"Object '{key}' does not exist in bucket '{bucket}'. Verify the object key and try again.",
                    Resource = $"{bucket}/{key}",
                    RequestId = requestId
                });
            }

            var stream = await objectStore.GetObjectAsync(bucket, key, cancellationToken);
            if (stream == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchKey",
                    Message = $"Object data for '{key}' not found in bucket '{bucket}'. The metadata exists but the file is missing.",
                    Resource = $"{bucket}/{key}",
                    RequestId = requestId
                });
            }

            return Results.File(stream, metadata.ContentType, enableRangeProcessing: true);
        }
        catch (IOException)
        {
            return Results.Problem(detail: $"I/O error downloading object '{key}' from bucket '{bucket}'. Check file system integrity.",
                statusCode: 500, title: "Storage Error", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ErrorType", "IOException" }
                });
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(detail: $"Download operation for object '{key}' was cancelled.",
                statusCode: 499, title: "Operation Cancelled", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") }
                });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: $"Failed to download object '{key}' from bucket '{bucket}': {ex.Message}. Request ID: {requestId}",
                statusCode: 500, title: "Download Failed", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ErrorType", ex.GetType().Name }
                });
        }
    }

    /// <summary>
    /// Gets object metadata.
    /// </summary>
    private static async Task<IResult> HeadObjectAsync(
        string bucket,
        string? key,
        HttpRequest request,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        var requestId = request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty. Provide a valid object key in the URL path.",
                Resource = $"{bucket}/",
                RequestId = requestId
            });
        }

        try
        {
            var metadata = await metadataStore.GetObjectMetadataAsync(bucket, key, cancellationToken);
            if (metadata == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchKey",
                    Message = $"Object '{key}' does not exist in bucket '{bucket}'. Verify the object key and try again.",
                    Resource = $"{bucket}/{key}",
                    RequestId = requestId
                });
            }

            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: $"Failed to get metadata for object '{key}' in bucket '{bucket}': {ex.Message}. Request ID: {requestId}",
                statusCode: 500, title: "Metadata Query Failed", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ErrorType", ex.GetType().Name }
                });
        }
    }

    /// <summary>
    /// Deletes an object.
    /// </summary>
    private static async Task<IResult> DeleteObjectAsync(
        string bucket,
        string? key,
        HttpRequest request,
        IMetadataStore metadataStore,
        IObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var requestId = request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty. Provide a valid object key in the URL path.",
                Resource = $"{bucket}/",
                RequestId = requestId
            });
        }

        try
        {
            var metadata = await metadataStore.GetObjectMetadataAsync(bucket, key, cancellationToken);
            if (metadata == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchKey",
                    Message = $"Object '{key}' does not exist in bucket '{bucket}'. Verify the object key and try again.",
                    Resource = $"{bucket}/{key}",
                    RequestId = requestId
                });
            }

            await objectStore.DeleteObjectAsync(bucket, key, cancellationToken);
            await metadataStore.DeleteObjectMetadataAsync(bucket, key, cancellationToken);
            await metadataStore.UpdateBucketStatisticsAsync(bucket, -1, -metadata.Size, cancellationToken);

            return Results.NoContent();
        }
        catch (IOException)
        {
            return Results.Problem(detail: $"I/O error deleting object '{key}' from bucket '{bucket}'. Check file system permissions.",
                statusCode: 500, title: "Storage Error", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ErrorType", "IOException" }
                });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: $"Failed to delete object '{key}' from bucket '{bucket}': {ex.Message}. Request ID: {requestId}",
                statusCode: 500, title: "Delete Failed", extensions: new Dictionary<string, object?>
                {
                    { "RequestId", requestId },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ErrorType", ex.GetType().Name }
                });
        }
    }

    /// <summary>
    /// Deletes multiple objects.
    /// </summary>
    private static async Task<IResult> DeleteMultipleObjectsAsync(
        string bucket,
        HttpRequest request,
        IMetadataStore metadataStore,
        IObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var deleted = new List<object>();
        var errors = new List<object>();

        try
        {
            // Parse XML body for DeleteMultipleObjects request
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);

            // Simple parsing - in production, use proper XML parser
            var keyMatches = System.Text.RegularExpressions.Regex.Matches(body, "<Key>([^<]+)</Key>");
            var keys = keyMatches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToList();

            foreach (var key in keys)
            {
                try
                {
                    var metadata = await metadataStore.GetObjectMetadataAsync(bucket, key, cancellationToken);
                    if (metadata != null)
                    {
                        await objectStore.DeleteObjectAsync(bucket, key, cancellationToken);
                        await metadataStore.DeleteObjectMetadataAsync(bucket, key, cancellationToken);
                        await metadataStore.UpdateBucketStatisticsAsync(bucket, -1, -metadata.Size, cancellationToken);
                        
                        deleted.Add(new
                        {
                            Key = key,
                            VersionId = metadata.VersionId,
                            DeleteMarker = metadata.IsDeleteMarker
                        });
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new
                    {
                        Key = key,
                        Code = "InternalError",
                        Message = $"Failed to delete object '{key}': {ex.Message}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to process delete request for bucket '{bucket}': {ex.Message}");
        }

        return Results.Ok(new
        {
            Deleted = deleted,
            Errors = errors
        });
    }

    /// <summary>
    /// Lists objects (ListObjectsV2).
    /// </summary>
    private static async Task<IResult> ListObjectsV2Async(
        string bucket,
        string? prefix,
        string? continuationToken,
        string? delimiter,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        try
        {
            var bucketExists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
            if (!bucketExists)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchBucket",
                    Message = $"Bucket '{bucket}' does not exist. Verify the bucket name and try again.",
                    Resource = bucket
                });
            }

            var (objects, commonPrefixes, nextToken) = await metadataStore.ListObjectsV2Async(
                bucket, prefix, delimiter, continuationToken, cancellationToken: cancellationToken);

            var response = new ListObjectsV2Response
            {
                Name = bucket,
                Prefix = prefix,
                Delimiter = delimiter,
                MaxKeys = 1000,
                IsTruncated = nextToken != null,
                NextContinuationToken = nextToken,
                Contents = objects.Select(o => new
                {
                    Key = o.Key,
                    LastModified = o.LastModified.ToString("o"),
                    ETag = o.ETag,
                    Size = o.Size,
                    StorageClass = o.StorageClass
                }).Cast<object>().ToList(),
                CommonPrefixes = commonPrefixes.Cast<object>().ToList(),
                KeyCount = objects.Count
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to list objects in bucket '{bucket}': {ex.Message}");
        }
    }

    /// <summary>
    /// ListObjectsV2 response.
    /// </summary>
    private sealed class ListObjectsV2Response
    {
        public string Name { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? Delimiter { get; set; }
        public int MaxKeys { get; set; }
        public bool IsTruncated { get; set; }
        public string? NextContinuationToken { get; set; }
        public List<object> Contents { get; set; } = new();
        public List<object> CommonPrefixes { get; set; } = new();
        public int KeyCount { get; set; }
        public string EncodingType { get; set; } = "url";
    }

    /// <summary>
    /// S3 error response.
    /// </summary>
    private sealed class S3Error
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Resource { get; set; }
        public string? RequestId { get; set; }
    }
}
