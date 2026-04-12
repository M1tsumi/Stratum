namespace Stratum.UnitTests.ValueObjects;

using Stratum.Domain.ValueObjects;
using Xunit;

public class ObjectKeyTests
{
    [Theory]
    [InlineData("file.txt")]
    [InlineData("path/to/file.txt")]
    [InlineData("folder/subfolder/file.txt")]
    [InlineData("a")]
    public void Create_ValidKey_ReturnsObjectKey(string validKey)
    {
        var objectKey = new ObjectKey(validKey);
        Assert.Equal(validKey, objectKey.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_InvalidKey_ThrowsArgumentException(string invalidKey)
    {
        Assert.Throws<ArgumentException>(() => new ObjectKey(invalidKey!));
    }

    [Fact]
    public void GetPrefix_ReturnsCorrectPrefix()
    {
        var key = new ObjectKey("path/to/file.txt");
        Assert.Equal("path/to", key.Prefix);
    }

    [Fact]
    public void GetPrefix_NoPrefix_ReturnsNull()
    {
        var key = new ObjectKey("file.txt");
        Assert.Null(key.Prefix);
    }

    [Fact]
    public void GetName_ReturnsCorrectName()
    {
        var key = new ObjectKey("path/to/file.txt");
        Assert.Equal("file.txt", key.Name);
    }

    [Fact]
    public void GetName_NoPath_ReturnsFileName()
    {
        var key = new ObjectKey("file.txt");
        Assert.Equal("file.txt", key.Name);
    }

    [Fact]
    public void StartsWith_ReturnsTrueForMatchingPrefix()
    {
        var key = new ObjectKey("path/to/file.txt");
        Assert.True(key.StartsWith("path/to"));
    }

    [Fact]
    public void StartsWith_ReturnsFalseForNonMatchingPrefix()
    {
        var key = new ObjectKey("path/to/file.txt");
        Assert.False(key.StartsWith("other/path"));
    }

    [Fact]
    public void IsInDirectory_ReturnsTrueForMatchingDirectory()
    {
        var key = new ObjectKey("path/to/file.txt");
        Assert.True(key.IsInDirectory("path/to"));
    }

    [Fact]
    public void IsInDirectory_ReturnsFalseForNonMatchingDirectory()
    {
        var key = new ObjectKey("path/to/file.txt");
        Assert.False(key.IsInDirectory("other/path"));
    }
}
