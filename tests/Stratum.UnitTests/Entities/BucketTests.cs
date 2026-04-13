namespace Stratum.UnitTests.Entities;

using Stratum.Domain.Entities;
using Xunit;

public class BucketTests
{
    [Fact]
    public void Create_ValidBucket_ReturnsBucket()
    {
        var bucket = new Bucket("my-bucket", "us-east-1");
        
        Assert.Equal("my-bucket", bucket.Name);
        Assert.Equal("us-east-1", bucket.Region);
        Assert.Equal(0, bucket.ObjectCount);
        Assert.Equal(0, bucket.TotalSize);
    }

    [Fact]
    public void Create_WithOwner_ReturnsBucketWithOwner()
    {
        var bucket = new Bucket("my-bucket", "us-east-1", "user-123", "Test User");
        
        Assert.Equal("user-123", bucket.OwnerId);
        Assert.Equal("Test User", bucket.OwnerDisplayName);
    }

    [Fact]
    public void UpdateStatistics_IncrementsCorrectly()
    {
        var bucket = new Bucket("my-bucket", "us-east-1");
        
        bucket.UpdateStatistics(5, 1024);
        
        Assert.Equal(5, bucket.ObjectCount);
        Assert.Equal(1024, bucket.TotalSize);
    }

    [Fact]
    public void UpdateStatistics_DecrementsCorrectly()
    {
        var bucket = new Bucket("my-bucket", "us-east-1");
        bucket.UpdateStatistics(10, 2048);
        
        bucket.UpdateStatistics(-3, -512);
        
        Assert.Equal(7, bucket.ObjectCount);
        Assert.Equal(1536, bucket.TotalSize);
    }
}
