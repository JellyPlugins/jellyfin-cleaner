using Jellyfin.Plugin.JellyfinHelper.Services;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests;

/// <summary>
/// Tests for <see cref="PathValidator"/>.
/// </summary>
public class PathValidatorTests
{
    // === IsSafePath ===

    [Fact]
    public void IsSafePath_NullPath_ReturnsFalse()
    {
        Assert.False(PathValidator.IsSafePath(null, "/base"));
    }

    [Fact]
    public void IsSafePath_EmptyPath_ReturnsFalse()
    {
        Assert.False(PathValidator.IsSafePath(string.Empty, "/base"));
    }

    [Fact]
    public void IsSafePath_WhitespacePath_ReturnsFalse()
    {
        Assert.False(PathValidator.IsSafePath("   ", "/base"));
    }

    [Fact]
    public void IsSafePath_PathWithTraversal_ReturnsFalse()
    {
        Assert.False(PathValidator.IsSafePath("/base/../etc/passwd", "/base"));
    }

    [Fact]
    public void IsSafePath_PathWithNullChar_ReturnsFalse()
    {
        Assert.False(PathValidator.IsSafePath("/base/file\0.txt", "/base"));
    }

    [Fact]
    public void IsSafePath_ValidChildPath_ReturnsTrue()
    {
        // Use a temp directory to ensure the path resolves correctly on this OS
        var baseDir = Path.GetTempPath();
        var childPath = Path.Combine(baseDir, "subdir", "file.txt");

        Assert.True(PathValidator.IsSafePath(childPath, baseDir));
    }

    [Fact]
    public void IsSafePath_PathOutsideBase_ReturnsFalse()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "specific-base");
        var outsidePath = Path.Combine(Path.GetTempPath(), "other-dir", "file.txt");

        Assert.False(PathValidator.IsSafePath(outsidePath, baseDir));
    }

    // === SanitizeFileName ===

    [Fact]
    public void SanitizeFileName_NullInput_ReturnsDefault()
    {
        Assert.Equal("export", PathValidator.SanitizeFileName(null!));
    }

    [Fact]
    public void SanitizeFileName_EmptyInput_ReturnsDefault()
    {
        Assert.Equal("export", PathValidator.SanitizeFileName(string.Empty));
    }

    [Fact]
    public void SanitizeFileName_ValidName_ReturnsSameName()
    {
        Assert.Equal("report.json", PathValidator.SanitizeFileName("report.json"));
    }

    [Fact]
    public void SanitizeFileName_StripsDirectorySeparators()
    {
        var result = PathValidator.SanitizeFileName("some/path/file.txt");
        Assert.Equal("file.txt", result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidChars()
    {
        // The '<' character is invalid in filenames on Windows
        var result = PathValidator.SanitizeFileName("file<name>.txt");
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
    }
}