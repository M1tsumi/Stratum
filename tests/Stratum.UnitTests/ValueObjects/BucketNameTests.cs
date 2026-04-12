namespace Stratum.UnitTests.ValueObjects;

using Stratum.Domain.ValueObjects;
using Xunit;

public class BucketNameTests
{
    [Theory]
    [InlineData("my-bucket")]
    [InlineData("mybucket")]
    [InlineData("my-bucket-123")]
    [InlineData("test-bucket-name")]
    public void Create_ValidBucketName_ReturnsBucketName(string validName)
    {
        var bucketName = new BucketName(validName);
        Assert.Equal(validName.ToLowerInvariant(), bucketName.Value);
    }

    [Theory]
    [InlineData("My-Bucket")]
    [InlineData("my_bucket")]
    [InlineData("my.bucket")]
    [InlineData("ab")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_InvalidBucketName_ThrowsArgumentException(string invalidName)
    {
        Assert.Throws<ArgumentException>(() => new BucketName(invalidName!));
    }

    [Theory]
    [InlineData("my-bucket", true)]
    [InlineData("mybucket", true)]
    [InlineData("My-Bucket", false)]
    [InlineData("my_bucket", false)]
    [InlineData("ab", false)]
    [InlineData("", false)]
    public void IsValid_ReturnsExpectedResult(string name, bool expected)
    {
        Assert.Equal(expected, BucketName.IsValid(name));
    }

    [Fact]
    public void TryCreate_ValidName_ReturnsBucketName()
    {
        var result = BucketName.TryCreate("my-bucket");
        Assert.NotNull(result);
        Assert.Equal("my-bucket", result.Value);
    }

    [Fact]
    public void TryCreate_InvalidName_ReturnsNull()
    {
        var result = BucketName.TryCreate("My-Bucket");
        Assert.Null(result);
    }
}
