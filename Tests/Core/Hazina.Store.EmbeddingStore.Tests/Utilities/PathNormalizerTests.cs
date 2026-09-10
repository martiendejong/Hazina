using Hazina.Store.EmbeddingStore.Utilities;
using Xunit;

namespace Hazina.Store.EmbeddingStore.Tests.Utilities;

public class PathNormalizerTests
{
    [Theory]
    [InlineData("path/to/file", "path\\to\\file")] // Unix to Windows
    [InlineData("path\\to\\file", "path\\to\\file")] // Windows format
    [InlineData("path/to\\mixed/file", "path\\to\\mixed\\file")] // Mixed separators
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Normalize_HandlesPathSeparators(string input, string expected)
    {
        // Adjust expected based on platform
        expected = expected?.Replace('\\', Path.DirectorySeparatorChar);

        var result = PathNormalizer.Normalize(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Combine_CombinesPathSegments()
    {
        var result = PathNormalizer.Combine("root", "folder", "file.txt");

        var expected = Path.Combine("root", "folder", "file.txt");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Combine_HandlesEmptySegments()
    {
        var result = PathNormalizer.Combine();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Combine_NormalizesAllSegments()
    {
        var result = PathNormalizer.Combine("root/folder", "sub\\folder", "file.txt");

        // Should normalize all segments and combine them
        Assert.Contains("root", result);
        Assert.Contains("folder", result);
        Assert.Contains("sub", result);
        Assert.Contains("file.txt", result);
    }

    [Fact]
    public void MakeAbsolute_ConvertsRelativeToAbsolute()
    {
        var relativePath = "folder/file.txt";

        var result = PathNormalizer.MakeAbsolute(relativePath);

        Assert.True(Path.IsPathRooted(result));
        Assert.Contains("folder", result);
        Assert.Contains("file.txt", result);
    }

    [Fact]
    public void MakeAbsolute_PreservesAbsolutePath()
    {
        var absolutePath = Path.GetFullPath("test");

        var result = PathNormalizer.MakeAbsolute(absolutePath);

        Assert.Equal(Path.GetFullPath(absolutePath), result);
    }

    [Fact]
    public void MakeAbsolute_WithBasePath_UsesBasePath()
    {
        var basePath = Path.GetTempPath();
        var relativePath = "folder/file.txt";

        var result = PathNormalizer.MakeAbsolute(relativePath, basePath);

        Assert.StartsWith(basePath.TrimEnd(Path.DirectorySeparatorChar),
                         result.TrimEnd(Path.DirectorySeparatorChar));
        Assert.Contains("folder", result);
    }

    [Theory]
    [InlineData("path\\to\\file", "path/to/file")]
    [InlineData("path/to/file", "path/to/file")]
    [InlineData("C:\\Windows\\System32", "C:/Windows/System32")]
    public void ToStorageFormat_ConvertsToForwardSlashes(string input, string expected)
    {
        var result = PathNormalizer.ToStorageFormat(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromStorageFormat_ConvertsToNativeSeparators()
    {
        var storagePath = "path/to/file";

        var result = PathNormalizer.FromStorageFormat(storagePath);

        var expected = storagePath.Replace('/', Path.DirectorySeparatorChar);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RoundTrip_ToStorageAndBack_PreservesPath()
    {
        var originalPath = PathNormalizer.Combine("root", "folder", "file.txt");

        var storagePath = PathNormalizer.ToStorageFormat(originalPath);
        var result = PathNormalizer.FromStorageFormat(storagePath);

        Assert.Equal(PathNormalizer.Normalize(originalPath), result);
    }
}
