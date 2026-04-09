using System;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.JellyfinHelper.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests;

public class TrashServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly Mock<ILogger> _loggerMock;

    public TrashServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "JellyfinHelperTests_Trash_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _loggerMock = new Mock<ILogger>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    // ===== TryParseTrashTimestamp Tests =====

    [Fact]
    public void TryParseTrashTimestamp_ValidFormat_ReturnsTrue()
    {
        var result = TrashService.TryParseTrashTimestamp("20260101-120000_MyMovie", out var timestamp);
        Assert.True(result);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), timestamp);
    }

    [Fact]
    public void TryParseTrashTimestamp_InvalidFormat_ReturnsFalse()
    {
        var result = TrashService.TryParseTrashTimestamp("not-a-timestamp_MyMovie", out _);
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void TryParseTrashTimestamp_EmptyOrShort_ReturnsFalse(string? input)
    {
        var result = TrashService.TryParseTrashTimestamp(input!, out _);
        Assert.False(result);
    }

    // ===== MoveToTrash Tests =====

    [Fact]
    public void MoveToTrash_NonExistentSource_ReturnsZero()
    {
        var trashPath = Path.Combine(_testRoot, "trash");
        var result = TrashService.MoveToTrash(
            Path.Combine(_testRoot, "nonexistent"),
            trashPath,
            _loggerMock.Object);

        Assert.Equal(0, result);
    }

    [Fact]
    public void MoveToTrash_ValidDirectory_MovesAndReturnsSize()
    {
        var sourceDir = Path.Combine(_testRoot, "source_movie");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllBytes(Path.Combine(sourceDir, "movie.mkv"), new byte[1024]);

        var trashPath = Path.Combine(_testRoot, "trash");

        var result = TrashService.MoveToTrash(sourceDir, trashPath, _loggerMock.Object);

        Assert.Equal(1024, result);
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(trashPath));

        // Verify trash folder contains one timestamped directory
        var trashDirs = Directory.GetDirectories(trashPath);
        Assert.Single(trashDirs);
        Assert.Contains("source_movie", Path.GetFileName(trashDirs[0]));
    }

    // ===== MoveFileToTrash Tests =====

    [Fact]
    public void MoveFileToTrash_NonExistentFile_ReturnsZero()
    {
        var trashPath = Path.Combine(_testRoot, "trash");
        var result = TrashService.MoveFileToTrash(
            Path.Combine(_testRoot, "nonexistent.srt"),
            trashPath,
            _loggerMock.Object);

        Assert.Equal(0, result);
    }

    [Fact]
    public void MoveFileToTrash_ValidFile_MovesAndReturnsSize()
    {
        var sourceFile = Path.Combine(_testRoot, "subtitle.srt");
        File.WriteAllBytes(sourceFile, new byte[512]);

        var trashPath = Path.Combine(_testRoot, "trash");

        var result = TrashService.MoveFileToTrash(sourceFile, trashPath, _loggerMock.Object);

        Assert.Equal(512, result);
        Assert.False(File.Exists(sourceFile));
        Assert.True(Directory.Exists(trashPath));

        var trashFiles = Directory.GetFiles(trashPath);
        Assert.Single(trashFiles);
        Assert.Contains("subtitle.srt", Path.GetFileName(trashFiles[0]));
    }

    // ===== PurgeExpiredTrash Tests =====

    [Fact]
    public void PurgeExpiredTrash_NonExistentTrashFolder_ReturnsZero()
    {
        var (bytesFreed, itemsPurged) = TrashService.PurgeExpiredTrash(
            Path.Combine(_testRoot, "nonexistent_trash"),
            7,
            _loggerMock.Object);

        Assert.Equal(0, bytesFreed);
        Assert.Equal(0, itemsPurged);
    }

    [Fact]
    public void PurgeExpiredTrash_NoExpiredItems_ReturnsZero()
    {
        var trashPath = Path.Combine(_testRoot, "trash");

        // Create a "fresh" trash item with current timestamp
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var freshDir = Path.Combine(trashPath, $"{timestamp}_RecentMovie");
        Directory.CreateDirectory(freshDir);
        File.WriteAllBytes(Path.Combine(freshDir, "movie.mkv"), new byte[100]);

        var (bytesFreed, itemsPurged) = TrashService.PurgeExpiredTrash(trashPath, 7, _loggerMock.Object);

        Assert.Equal(0, bytesFreed);
        Assert.Equal(0, itemsPurged);
    }

    [Fact]
    public void PurgeExpiredTrash_ExpiredDirectory_PurgesIt()
    {
        var trashPath = Path.Combine(_testRoot, "trash");

        // Create an "expired" trash item with old timestamp
        var oldTimestamp = DateTime.UtcNow.AddDays(-10).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var oldDir = Path.Combine(trashPath, $"{oldTimestamp}_OldMovie");
        Directory.CreateDirectory(oldDir);
        File.WriteAllBytes(Path.Combine(oldDir, "movie.mkv"), new byte[256]);

        var (bytesFreed, itemsPurged) = TrashService.PurgeExpiredTrash(trashPath, 7, _loggerMock.Object);

        Assert.Equal(256, bytesFreed);
        Assert.Equal(1, itemsPurged);
        Assert.False(Directory.Exists(oldDir));
    }

    [Fact]
    public void PurgeExpiredTrash_ExpiredFile_PurgesIt()
    {
        var trashPath = Path.Combine(_testRoot, "trash");
        Directory.CreateDirectory(trashPath);

        var oldTimestamp = DateTime.UtcNow.AddDays(-15).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var oldFile = Path.Combine(trashPath, $"{oldTimestamp}_old.srt");
        File.WriteAllBytes(oldFile, new byte[128]);

        var (bytesFreed, itemsPurged) = TrashService.PurgeExpiredTrash(trashPath, 7, _loggerMock.Object);

        Assert.Equal(128, bytesFreed);
        Assert.Equal(1, itemsPurged);
        Assert.False(File.Exists(oldFile));
    }

    // ===== GetTrashSummary Tests =====

    [Fact]
    public void GetTrashSummary_NonExistentFolder_ReturnsZero()
    {
        var (totalSize, itemCount) = TrashService.GetTrashSummary(Path.Combine(_testRoot, "nonexistent"));
        Assert.Equal(0, totalSize);
        Assert.Equal(0, itemCount);
    }

    [Fact]
    public void GetTrashSummary_WithItems_ReturnsSizeAndCount()
    {
        var trashPath = Path.Combine(_testRoot, "trash");

        // Directory item
        var dir1 = Path.Combine(trashPath, "20260101-120000_Movie1");
        Directory.CreateDirectory(dir1);
        File.WriteAllBytes(Path.Combine(dir1, "movie.mkv"), new byte[1000]);

        // File item
        var file1 = Path.Combine(trashPath, "20260101-130000_sub.srt");
        File.WriteAllBytes(file1, new byte[500]);

        var (totalSize, itemCount) = TrashService.GetTrashSummary(trashPath);

        Assert.Equal(1500, totalSize);
        Assert.Equal(2, itemCount);
    }
}