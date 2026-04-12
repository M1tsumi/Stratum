namespace Stratum.UnitTests.Services;

using Stratum.Domain.Services;
using Xunit;

public class ObjectKeyValidatorTests
{
    [Theory]
    [InlineData("file.txt")]
    [InlineData("path/to/file.txt")]
    [InlineData("folder/subfolder/file.txt")]
    public void IsValid_ValidKeys_ReturnsTrue(string validKey)
    {
        Assert.True(ObjectKeyValidator.IsValid(validKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_InvalidKeys_ReturnsFalse(string invalidKey)
    {
        Assert.False(ObjectKeyValidator.IsValid(invalidKey!));
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("path/to/file.txt")]
    public void Validate_ValidKeys_DoesNotThrow(string validKey)
    {
        ObjectKeyValidator.Validate(validKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_InvalidKeys_ThrowsArgumentException(string invalidKey)
    {
        Assert.Throws<ArgumentException>(() => ObjectKeyValidator.Validate(invalidKey!));
    }

    [Theory]
    [InlineData("/file.txt", "file.txt")]
    [InlineData("path/to/file.txt", "path/to/file.txt")]
    public void Normalize_RemovesLeadingSlash(string input, string expected)
    {
        var result = ObjectKeyValidator.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("path/to/file.txt", "path/to")]
    [InlineData("file.txt", null)]
    public void GetPrefix_ReturnsCorrectPrefix(string key, string? expected)
    {
        var result = ObjectKeyValidator.GetPrefix(key);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("path/to/file.txt", "file.txt")]
    [InlineData("file.txt", "file.txt")]
    public void GetName_ReturnsCorrectName(string key, string expected)
    {
        var result = ObjectKeyValidator.GetName(key);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsInDirectory_ReturnsTrueForMatchingDirectory()
    {
        Assert.True(ObjectKeyValidator.IsInDirectory("path/to/file.txt", "path/to"));
    }

    [Fact]
    public void IsInDirectory_ReturnsFalseForNonMatchingDirectory()
    {
        Assert.False(ObjectKeyValidator.IsInDirectory("path/to/file.txt", "other/path"));
    }

    [Fact]
    public void MatchesPrefix_ReturnsTrueForMatchingPrefix()
    {
        Assert.True(ObjectKeyValidator.MatchesPrefix("path/to/file.txt", "path/to"));
    }

    [Fact]
    public void MatchesPrefix_ReturnsFalseForNonMatchingPrefix()
    {
        Assert.False(ObjectKeyValidator.MatchesPrefix("path/to/file.txt", "other/path"));
    }
}
