namespace Stratum.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Stratum.Domain.Entities;
using Stratum.Domain.Interfaces;
using Stratum.Domain.Services;

/// <summary>
/// S3 multipart upload API endpoints.
/// </summary>
public static class MultipartEndpoints
{
    /// <summary>
    /// Registers multipart endpoints.
    /// </summary>
    public static void MapMultipartEndpoints(this IEndpointRouteBuilder app)
    {
        // CreateMultipartUpload - use specific path to avoid conflict
        app.MapPost("/{bucket}/{*key}/uploads", CreateMultipartUploadAsync)
            .WithName("CreateMultipartUpload");

        // UploadPart - use specific path to avoid conflict
        app.MapPut("/{bucket}/{*key}/upload", UploadPartAsync)
            .WithName("UploadPart");

        // CompleteMultipartUpload - use specific path to avoid conflict
        app.MapPost("/{bucket}/{*key}/complete", CompleteMultipartUploadAsync)
            .WithName("CompleteMultipartUpload");

        // AbortMultipartUpload - use specific path to avoid conflict
        app.MapDelete("/{bucket}/{*key}/abort", AbortMultipartUploadAsync)
            .WithName("AbortMultipartUpload");

        // ListMultipartUploads
        app.MapGet("/{bucket}/uploads", ListMultipartUploadsAsync)
            .WithName("ListMultipartUploads");

        // ListParts - query parameters only
        app.MapGet("/{bucket}/parts", ListPartsAsync)
            .WithName("ListParts");
    }

    /// <summary>
    /// Initiates a multipart upload for an object.
    /// </summary>
    private static async Task<IResult> CreateMultipartUploadAsync(
        string bucket,
        string? key,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        // Remove /uploads suffix from key
        if (!string.IsNullOrEmpty(key) && key.EndsWith("/uploads", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^8];
        }

        if (string.IsNullOrEmpty(key))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key cannot be empty. Provide a valid object key.",
                Resource = $"{bucket}/"
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
                    Message = $"Bucket '{bucket}' does not exist. Create the bucket first.",
                    Resource = bucket
                });
            }

            var uploadId = Guid.NewGuid().ToString();
            var upload = new MultipartUpload(
                uploadId,
                bucket,
                key,
                DateTime.UtcNow,
                null,
                null,
                null,
                new Dictionary<string, string>(),
                "STANDARD",
                new List<MultipartPart>());

            await metadataStore.CreateMultipartUploadAsync(upload, cancellationToken);

            return Results.Ok(new
            {
                Bucket = bucket,
                Key = key,
                UploadId = uploadId
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to create multipart upload for object '{key}' in bucket '{bucket}': {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads a part in a multipart upload.
    /// </summary>
    private static async Task<IResult> UploadPartAsync(
        string bucket,
        string? key,
        [FromQuery] string? uploadId,
        [FromQuery] int partNumber,
        HttpRequest request,
        IObjectStore objectStore,
        IMetadataStore metadataStore,
        ETagCalculator eTagCalculator,
        CancellationToken cancellationToken)
    {
        // Remove /upload suffix from key
        if (!string.IsNullOrEmpty(key) && key.EndsWith("/upload", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^7];
        }

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(uploadId))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key and upload ID are required. Provide both parameters.",
                Resource = $"{bucket}/"
            });
        }

        try
        {
            var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
            if (upload == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchUpload",
                    Message = $"Multipart upload '{uploadId}' does not exist. Verify the upload ID and try again.",
                    Resource = $"{bucket}/{key}"
                });
            }

            var eTag = await eTagCalculator.CalculateSinglePartETagAsync(request.Body, cancellationToken);
            var size = request.ContentLength ?? 0;

            var partKey = $"{uploadId}/part{partNumber}";
            await objectStore.PutObjectAsync(bucket, partKey, request.Body, cancellationToken);

            var part = new MultipartPart(partNumber, $"\"{eTag}\"", size, DateTime.UtcNow);
            await metadataStore.AddPartAsync(uploadId, part, cancellationToken);

            return Results.Ok(new
            {
                ETag = $"\"{eTag}\""
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to upload part {partNumber} for object '{key}' in bucket '{bucket}': {ex.Message}");
        }
    }

    /// <summary>
    /// Completes a multipart upload by assembling the uploaded parts.
    /// </summary>
    private static async Task<IResult> CompleteMultipartUploadAsync(
        string bucket,
        string? key,
        [FromQuery] string? uploadId,
        IObjectStore objectStore,
        IMetadataStore metadataStore,
        ETagCalculator eTagCalculator,
        CancellationToken cancellationToken)
    {
        // Remove /complete suffix from key
        if (!string.IsNullOrEmpty(key) && key.EndsWith("/complete", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^8];
        }

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(uploadId))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key and upload ID are required. Provide both parameters.",
                Resource = $"{bucket}/"
            });
        }

        try
        {
            var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
            if (upload == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchUpload",
                    Message = $"Multipart upload '{uploadId}' does not exist. Verify the upload ID and try again.",
                    Resource = $"{bucket}/{key}"
                });
            }

            var partETags = upload.Parts.Select(p => p.ETag.Trim('"')).ToList();
            var combinedETag = eTagCalculator.CalculateMultipartETag(partETags);

            using var combinedStream = new MemoryStream();
            foreach (var part in upload.Parts.OrderBy(p => p.PartNumber))
            {
                var partKey = $"{uploadId}/part{part.PartNumber}";
                var partStream = await objectStore.GetObjectAsync(bucket, partKey, cancellationToken);
                if (partStream != null)
                {
                    await partStream.CopyToAsync(combinedStream, cancellationToken);
                    await objectStore.DeleteObjectAsync(bucket, partKey, cancellationToken);
                }
            }

            combinedStream.Position = 0;
            await objectStore.PutObjectAsync(bucket, key, combinedStream, cancellationToken);
            await metadataStore.CompleteMultipartUploadAsync(uploadId, cancellationToken);

            return Results.Ok(new
            {
                Location = $"/{bucket}/{key}",
                Bucket = bucket,
                Key = key,
                ETag = $"\"{combinedETag}\""
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to complete multipart upload '{uploadId}' for object '{key}' in bucket '{bucket}': {ex.Message}");
        }
    }

    /// <summary>
    /// Aborts a multipart upload.
    /// </summary>
    private static async Task<IResult> AbortMultipartUploadAsync(
        string bucket,
        string? key,
        [FromQuery] string? uploadId,
        IObjectStore objectStore,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        // Remove /abort suffix from key
        if (!string.IsNullOrEmpty(key) && key.EndsWith("/abort", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^5];
        }

        if (string.IsNullOrEmpty(uploadId))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Upload ID is required. Provide a valid upload ID.",
                Resource = $"{bucket}/"
            });
        }

        try
        {
            var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
            if (upload == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchUpload",
                    Message = $"Multipart upload '{uploadId}' does not exist. Verify the upload ID and try again.",
                    Resource = $"{bucket}/{key}"
                });
            }

            foreach (var part in upload.Parts)
            {
                var partKey = $"{uploadId}/part{part.PartNumber}";
                await objectStore.DeleteObjectAsync(bucket, partKey, cancellationToken);
            }

            await metadataStore.AbortMultipartUploadAsync(uploadId, cancellationToken);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to abort multipart upload '{uploadId}' for object '{key}' in bucket '{bucket}': {ex.Message}");
        }
    }

    /// <summary>
    /// Lists all in-progress multipart uploads in a bucket.
    /// </summary>
    private static async Task<IResult> ListMultipartUploadsAsync(
        string bucket,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken,
        [FromQuery] string? prefix = null,
        [FromQuery] bool uploads = false)
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

            var uploadList = await metadataStore.ListMultipartUploadsAsync(bucket, prefix, cancellationToken);

            return Results.Ok(new
            {
                Bucket = bucket,
                Uploads = uploadList.Select(u => new
                {
                    Key = u.Key,
                    UploadId = u.UploadId,
                    Initiated = u.InitiatedAt.ToString("o"),
                    StorageClass = u.StorageClass
                }),
                CommonPrefixes = new List<object>()
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to list multipart uploads in bucket '{bucket}': {ex.Message}");
        }
    }

    /// <summary>
    /// Lists the parts that have been uploaded for a specific multipart upload.
    /// </summary>
    private static async Task<IResult> ListPartsAsync(
        string bucket,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken,
        [FromQuery] string? key = null,
        [FromQuery] string? uploadId = null)
    {
        if (string.IsNullOrEmpty(uploadId))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Upload ID is required. Provide a valid upload ID.",
                Resource = $"{bucket}/"
            });
        }

        try
        {
            var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
            if (upload == null)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchUpload",
                    Message = $"Multipart upload '{uploadId}' does not exist. Verify the upload ID and try again.",
                    Resource = $"{bucket}/{key}"
                });
            }

            return Results.Ok(new
            {
                Bucket = bucket,
                Key = upload.Key,
                UploadId = uploadId,
                Parts = upload.Parts.Select(p => new
                {
                    PartNumber = p.PartNumber,
                    ETag = p.ETag,
                    Size = p.Size,
                    LastModified = p.LastModified.ToString("o")
                })
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to list parts for multipart upload '{uploadId}' in bucket '{bucket}': {ex.Message}");
        }
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
