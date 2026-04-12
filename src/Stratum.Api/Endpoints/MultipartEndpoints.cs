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
        // CreateMultipartUpload - use different path to avoid conflict
        app.MapPost("/{bucket}/{*key}/uploads", CreateMultipartUploadAsync)
            .WithName("CreateMultipartUpload");

        // UploadPart - use different path to avoid conflict
        app.MapPut("/{bucket}/{*key}/upload", UploadPartAsync)
            .WithName("UploadPart");

        // CompleteMultipartUpload - use different path to avoid conflict
        app.MapPost("/{bucket}/{*key}/complete", CompleteMultipartUploadAsync)
            .WithName("CompleteMultipartUpload");

        // AbortMultipartUpload - use different path to avoid conflict
        app.MapDelete("/{bucket}/{*key}/abort", AbortMultipartUploadAsync)
            .WithName("AbortMultipartUpload");

        // ListMultipartUploads
        app.MapGet("/{bucket}/uploads", ListMultipartUploadsAsync)
            .WithName("ListMultipartUploads");

        // ListParts - remove catch-all from middle of route
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
        CancellationToken cancellationToken,
        [FromQuery] bool uploads = false)
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
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(uploadId))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key and upload ID are required.",
                Resource = $"{bucket}/"
            });
        }

        var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchUpload",
                Message = "The specified multipart upload does not exist.",
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
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(uploadId))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Object key and upload ID are required.",
                Resource = $"{bucket}/"
            });
        }

        var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchUpload",
                Message = "The specified multipart upload does not exist.",
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
        if (string.IsNullOrEmpty(uploadId))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidRequest",
                Message = "Upload ID is required.",
                Resource = $"{bucket}/"
            });
        }

        var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchUpload",
                Message = "The specified multipart upload does not exist.",
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
                Message = "Upload ID is required.",
                Resource = $"{bucket}/"
            });
        }

        var upload = await metadataStore.GetMultipartUploadAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchUpload",
                Message = "The specified multipart upload does not exist.",
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
