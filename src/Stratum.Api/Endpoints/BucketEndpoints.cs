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
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidBucketName",
                Message = "Bucket name cannot be empty or whitespace.",
                Resource = bucket
            });
        }

        if (!IsValidBucketName(bucket))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidBucketName",
                Message = $"Bucket name '{bucket}' is not valid. Bucket names must: be between 3-63 characters, start/end with alphanumeric, contain only lowercase letters, numbers, hyphens, and dots, and not be formatted as an IP address.",
                Resource = bucket
            });
        }

        try
        {
            var exists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
            if (exists)
            {
                return Results.Conflict(new S3Error
                {
                    Code = "BucketAlreadyExists",
                    Message = $"Bucket '{bucket}' already exists. Choose a different bucket name.",
                    Resource = bucket
                });
            }

            var newBucket = new Bucket(bucket, "us-east-1");
            await metadataStore.CreateBucketAsync(newBucket, cancellationToken);

            context.Response.Headers.Location = $"/{bucket}";
            return Results.Ok();
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499); // Client Closed Request
        }
        catch (IOException ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Storage operation failed: {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new S3Error
            {
                Code = "AccessDenied",
                Message = $"Permission denied: {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 403);
        }
        catch (Exception ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Failed to create bucket '{bucket}': {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
    }

    /// <summary>
    /// Deletes a bucket.
    /// </summary>
    private static async Task<IResult> DeleteBucketAsync(
        string bucket,
        HttpContext context,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidBucketName",
                Message = "Bucket name cannot be empty or whitespace.",
                Resource = bucket
            });
        }

        try
        {
            var exists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
            if (!exists)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchBucket",
                    Message = $"Bucket '{bucket}' does not exist. Verify the bucket name and try again.",
                    Resource = bucket,
                    RequestId = context.TraceIdentifier
                });
            }

            await metadataStore.DeleteBucketAsync(bucket, cancellationToken);
            return Results.NoContent();
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499); // Client Closed Request
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not empty"))
        {
            return Results.Json(new S3Error
            {
                Code = "BucketNotEmpty",
                Message = $"Bucket '{bucket}' is not empty. Delete all objects in the bucket before deleting the bucket.",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 409);
        }
        catch (IOException ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Storage operation failed: {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new S3Error
            {
                Code = "AccessDenied",
                Message = $"Permission denied: {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 403);
        }
        catch (Exception ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Failed to delete bucket '{bucket}': {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
    }

    /// <summary>
    /// Checks bucket existence.
    /// </summary>
    private static async Task<IResult> HeadBucketAsync(
        string bucket,
        HttpContext context,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return Results.BadRequest(new S3Error
            {
                Code = "InvalidBucketName",
                Message = "Bucket name cannot be empty or whitespace.",
                Resource = bucket
            });
        }

        try
        {
            var exists = await metadataStore.BucketExistsAsync(bucket, cancellationToken);
            if (!exists)
            {
                return Results.NotFound(new S3Error
                {
                    Code = "NoSuchBucket",
                    Message = $"Bucket '{bucket}' does not exist. Verify the bucket name and try again.",
                    Resource = bucket,
                    RequestId = context.TraceIdentifier
                });
            }

            return Results.Ok();
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499); // Client Closed Request
        }
        catch (IOException ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Storage operation failed: {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
        catch (Exception ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Failed to check bucket '{bucket}': {ex.Message}",
                Resource = bucket,
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
    }

    /// <summary>
    /// Lists all buckets.
    /// </summary>
    private static async Task<IResult> ListBucketsAsync(
        HttpContext context,
        IMetadataStore metadataStore,
        CancellationToken cancellationToken)
    {
        try
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
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499); // Client Closed Request
        }
        catch (IOException ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Storage operation failed: {ex.Message}",
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
        catch (Exception ex)
        {
            return Results.Json(new S3Error
            {
                Code = "InternalError",
                Message = $"Failed to list buckets: {ex.Message}",
                RequestId = context.TraceIdentifier
            }, statusCode: 500);
        }
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
