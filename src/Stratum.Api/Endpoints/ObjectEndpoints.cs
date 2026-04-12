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
        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty.",
                Resource = $"{bucket}/"
            });
        }

        var bucketExists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
        if (!bucketExists)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchBucket",
                Message = "The specified bucket does not exist.",
                Resource = bucket
            });
        }

        var contentType = request.ContentType ?? "application/octet-stream";
        var eTag = await eTagCalculator.CalculateSinglePartETagAsync(request.Body, cancellationToken);

        await objectStore.PutObjectAsync(bucket, key, request.Body, cancellationToken);
        
        var metadata = new ObjectMetadata(
            bucket,
            key,
            $"\"{eTag}\"",
            request.ContentLength ?? 0,
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

    /// <summary>
    /// Downloads an object.
    /// </summary>
    private static async Task<IResult> GetObjectAsync(
        string bucket,
        string? key,
        IMetadataStore metadataStore,
        IObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty.",
                Resource = $"{bucket}/"
            });
        }

        var metadata = await metadataStore.GetObjectMetadataAsync(bucket, key, cancellationToken);
        if (metadata == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchKey",
                Message = "The specified key does not exist.",
                Resource = $"{bucket}/{key}"
            });
        }

        var stream = await objectStore.GetObjectAsync(bucket, key, cancellationToken);
        if (stream == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchKey",
                Message = "The specified key does not exist.",
                Resource = $"{bucket}/{key}"
            });
        }

        return Results.File(stream, metadata.ContentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Gets object metadata.
    /// </summary>
    private static async Task<IResult> HeadObjectAsync(
        string bucket,
        string? key,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty.",
                Resource = $"{bucket}/"
            });
        }

        var metadata = await metadataStore.GetObjectMetadataAsync(bucket, key, cancellationToken);
        if (metadata == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchKey",
                Message = "The specified key does not exist.",
                Resource = $"{bucket}/{key}"
            });
        }

        return Results.Ok();
    }

    /// <summary>
    /// Deletes an object.
    /// </summary>
    private static async Task<IResult> DeleteObjectAsync(
        string bucket,
        string? key,
        IMetadataStore metadataStore,
        IObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty.",
                Resource = $"{bucket}/"
            });
        }

        var metadata = await metadataStore.GetObjectMetadataAsync(bucket, key, cancellationToken);
        if (metadata == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchKey",
                Message = "The specified key does not exist.",
                Resource = $"{bucket}/{key}"
            });
        }

        await objectStore.DeleteObjectAsync(bucket, key, cancellationToken);
        await metadataStore.DeleteObjectMetadataAsync(bucket, key, cancellationToken);
        await metadataStore.UpdateBucketStatisticsAsync(bucket, -1, -metadata.Size, cancellationToken);

        return Results.NoContent();
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
            catch (Exception)
            {
                errors.Add(new
                {
                    Key = key,
                    Code = "InternalError",
                    Message = "Failed to delete object"
                });
            }
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
        IMetadataStore metadataStore,
        CancellationToken cancellationToken,
        [FromQuery] string? prefix = null,
        [FromQuery] string? delimiter = null,
        [FromQuery] string? continuationToken = null,
        [FromQuery] int maxKeys = 1000,
        [FromQuery] string encodingType = "url")
    {
        var bucketExists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
        if (!bucketExists)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchBucket",
                Message = "The specified bucket does not exist.",
                Resource = bucket
            });
        }

        var (objects, commonPrefixes, nextToken) = await metadataStore.ListObjectsV2Async(
            bucket, prefix, delimiter, continuationToken, maxKeys, cancellationToken);

        var response = new ListObjectsV2Response
        {
            Name = bucket,
            Prefix = prefix,
            Delimiter = delimiter,
            MaxKeys = maxKeys,
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
            KeyCount = objects.Count,
            EncodingType = encodingType
        };

        return Results.Ok(response);
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
