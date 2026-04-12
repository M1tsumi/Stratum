namespace Stratum.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Stratum.Domain.Entities;
using Stratum.Domain.Interfaces;

/// <summary>
/// S3 bucket API endpoints.
/// </summary>
public static class BucketEndpoints
{
    /// <summary>
    /// Registers bucket endpoints.
    /// </summary>
    public static void MapBucketEndpoints(this IEndpointRouteBuilder app)
    {
        // CreateBucket
        app.MapPut("/{bucket}", CreateBucketAsync)
            .WithName("CreateBucket");

        // DeleteBucket
        app.MapDelete("/{bucket}", DeleteBucketAsync)
            .WithName("DeleteBucket");

        // HeadBucket
        app.MapMethods("/{bucket}", new[] { "HEAD" }, HeadBucketAsync)
            .WithName("HeadBucket");

        // ListBuckets (service-level operation)
        app.MapGet("/", ListBucketsAsync)
            .WithName("ListBuckets");
    }

    /// <summary>
    /// Creates a bucket.
    /// </summary>
    private static async Task<IResult> CreateBucketAsync(
        string bucket,
        HttpContext context,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        if (!IsValidBucketName(bucket))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidBucketName",
                Message = "The specified bucket name is not valid.",
                Resource = bucket
            });
        }

        var exists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
        if (exists)
        {
            return Results.Conflict(new S3Error
            {
                Code = "BucketAlreadyExists",
                Message = "The specified bucket already exists.",
                Resource = bucket
            });
        }

        var newBucket = new Bucket(bucket, "us-east-1");
        await metadataStore.CreateBucketAsync(newBucket, cancellationToken);
        
        context.Response.Headers.Location = $"/{bucket}";
        return Results.Ok();
    }

    /// <summary>
    /// Deletes a bucket.
    /// </summary>
    private static async Task<IResult> DeleteBucketAsync(
        string bucket,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        var exists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
        if (!exists)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchBucket",
                Message = "The specified bucket does not exist.",
                Resource = bucket
            });
        }

        await metadataStore.DeleteBucketAsync(bucket, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Checks bucket existence.
    /// </summary>
    private static async Task<IResult> HeadBucketAsync(
        string bucket,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        var bucketData = await metadataStore.GetBucketAsync(bucket, cancellationToken);
        if (bucketData == null)
        {
            return Results.NotFound(new S3Error
            {
                Code = "NoSuchBucket",
                Message = "The specified bucket does not exist.",
                Resource = bucket
            });
        }

        return Results.Ok();
    }

    /// <summary>
    /// Lists all buckets.
    /// </summary>
    private static async Task<IResult> ListBucketsAsync(
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        var buckets = await metadataStore.ListBucketsAsync(cancellationToken);

        return Results.Ok(new
        {
            Buckets = buckets.Select(b => new
            {
                Name = b.Name,
                CreationDate = b.CreatedAt.ToString("o")
            }),
            Owner = new
            {
                ID = "stratum",
                DisplayName = "Stratum"
            }
        });
    }

    /// <summary>
    /// Validates bucket name format.
    /// </summary>
    private static bool IsValidBucketName(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return false;
        }

        // Length check
        if (bucketName.Length < 3 || bucketName.Length > 63)
        {
            return false;
        }

        // Character set check (lowercase letters, numbers, hyphens only)
        if (!bucketName.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-'))
        {
            return false;
        }

        // Must start and end with letter or number
        if (!char.IsLetterOrDigit(bucketName[0]) || !char.IsLetterOrDigit(bucketName[^1]))
        {
            return false;
        }

        // Must not contain consecutive hyphens
        if (bucketName.Contains("--"))
        {
            return false;
        }

        // Must not be formatted as an IP address
        if (System.Net.IPAddress.TryParse(bucketName, out _))
        {
            return false;
        }

        return true;
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
