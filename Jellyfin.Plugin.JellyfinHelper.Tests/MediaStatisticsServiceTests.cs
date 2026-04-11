using Jellyfin.Plugin.JellyfinHelper.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests;

public class MediaStatisticsServiceTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<IFileSystem> _fileSystemMock;
    private readonly Mock<ILogger<MediaStatisticsService>> _loggerMock;
    private readonly MediaStatisticsService _service;

    public MediaStatisticsServiceTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _fileSystemMock = new Mock<IFileSystem>();
        _loggerMock = new Mock<ILogger<MediaStatisticsService>>();
        _service = new MediaStatisticsService(_libraryManagerMock.Object, _fileSystemMock.Object, _loggerMock.Object);
    }

    private static string TestPath(params string[] segments)
        => Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar, segments);

    [Fact]
    public void CalculateStatistics_NoLibraries_ReturnsEmptyResult()
    {
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Empty(result.Libraries);
        Assert.Empty(result.Movies);
        Assert.Empty(result.TvShows);
        Assert.Empty(result.Music);
        Assert.Empty(result.Other);
        Assert.Equal(0, result.TotalMovieVideoSize);
        Assert.Equal(0, result.TotalTvShowVideoSize);
        Assert.Equal(0, result.TotalTrickplaySize);
    }

    [Fact]
    public void CalculateStatistics_MovieLibrary_ClassifiesVideoFiles()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var mkvFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.mkv"),
            Name = "Film.mkv",
            Length = 1_500_000_000,
            IsDirectory = false
        };

        var mp4File = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film2.mp4"),
            Name = "Film2.mp4",
            Length = 2_000_000_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([mkvFile, mp4File]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Single(result.Libraries);
        Assert.Single(result.Movies);
        Assert.Empty(result.TvShows);
        Assert.Equal(3_500_000_000, result.TotalMovieVideoSize);
        Assert.Equal(2, result.Libraries[0].VideoFileCount);
    }

    [Fact]
    public void CalculateStatistics_TvShowLibrary_ClassifiesCorrectly()
    {
        var libraryPath = TestPath("media", "tv");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "TV Shows",
            CollectionType = CollectionTypeOptions.tvshows,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var videoFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "tv", "Episode.mkv"),
            Name = "Episode.mkv",
            Length = 500_000_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([videoFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Single(result.TvShows);
        Assert.Empty(result.Movies);
        Assert.Equal(500_000_000, result.TotalTvShowVideoSize);
    }

    [Fact]
    public void CalculateStatistics_MusicLibrary_ClassifiesAudioFiles()
    {
        var libraryPath = TestPath("media", "music");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Music",
            CollectionType = CollectionTypeOptions.music,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var flacFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "music", "Song.flac"),
            Name = "Song.flac",
            Length = 30_000_000,
            IsDirectory = false
        };

        var mp3File = new FileSystemMetadata
        {
            FullName = TestPath("media", "music", "Track.mp3"),
            Name = "Track.mp3",
            Length = 5_000_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([flacFile, mp3File]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Single(result.Music);
        Assert.Equal(35_000_000, result.TotalMusicAudioSize);
        Assert.Equal(2, result.Libraries[0].AudioFileCount);
    }

    [Fact]
    public void CalculateStatistics_SubtitleFiles_CountedCorrectly()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var srtFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.srt"),
            Name = "Film.srt",
            Length = 50_000,
            IsDirectory = false
        };

        var assFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.ass"),
            Name = "Film.ass",
            Length = 80_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([srtFile, assFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(130_000, result.TotalSubtitleSize);
        Assert.Equal(2, result.Libraries[0].SubtitleFileCount);
    }

    [Fact]
    public void CalculateStatistics_ImageFiles_CountedCorrectly()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var jpgFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "poster.jpg"),
            Name = "poster.jpg",
            Length = 200_000,
            IsDirectory = false
        };

        var pngFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "backdrop.png"),
            Name = "backdrop.png",
            Length = 500_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([jpgFile, pngFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(700_000, result.TotalImageSize);
        Assert.Equal(2, result.Libraries[0].ImageFileCount);
    }

    [Fact]
    public void CalculateStatistics_NfoFiles_CountedCorrectly()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var nfoFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.nfo"),
            Name = "Film.nfo",
            Length = 10_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([nfoFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(10_000, result.TotalNfoSize);
        Assert.Equal(1, result.Libraries[0].NfoFileCount);
    }

    [Fact]
    public void CalculateStatistics_TrickplayFolder_SizeCalculated()
    {
        var libraryPath = TestPath("media", "movies");
        var trickplayPath = TestPath("media", "movies", "Film.trickplay");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([]);

        var trickplayDir = new FileSystemMetadata
        {
            FullName = trickplayPath,
            Name = "Film.trickplay",
            IsDirectory = true
        };
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([trickplayDir]);

        // Trickplay folder content
        var trickplayFile1 = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.trickplay", "001.jpg"),
            Name = "001.jpg",
            Length = 25_000,
            IsDirectory = false
        };
        var trickplayFile2 = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.trickplay", "002.jpg"),
            Name = "002.jpg",
            Length = 25_000,
            IsDirectory = false
        };
        _fileSystemMock.Setup(f => f.GetFiles(trickplayPath, false)).Returns([trickplayFile1, trickplayFile2]);
        _fileSystemMock.Setup(f => f.GetDirectories(trickplayPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(50_000, result.TotalTrickplaySize);
        Assert.Equal(1, result.Libraries[0].TrickplayFolderCount);
    }

    [Fact]
    public void CalculateStatistics_UnrecognizedFiles_CountedAsOther()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var txtFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "readme.txt"),
            Name = "readme.txt",
            Length = 1_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([txtFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(1_000, result.Libraries[0].OtherSize);
        Assert.Equal(1, result.Libraries[0].OtherFileCount);
    }

    [Fact]
    public void CalculateStatistics_RecursiveDirectoryTraversal_AccumulatesSizes()
    {
        var libraryPath = TestPath("media", "movies");
        var subDirPath = TestPath("media", "movies", "Film");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        // Root has no files
        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([]);

        var subDir = new FileSystemMetadata
        {
            FullName = subDirPath,
            Name = "Film",
            IsDirectory = true
        };
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([subDir]);

        // Subdirectory has a video file
        var videoFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film", "Film.mkv"),
            Name = "Film.mkv",
            Length = 4_000_000_000,
            IsDirectory = false
        };
        _fileSystemMock.Setup(f => f.GetFiles(subDirPath, false)).Returns([videoFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(subDirPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(4_000_000_000, result.TotalMovieVideoSize);
    }

    [Fact]
    public void CalculateStatistics_IoExceptionInDirectory_ContinuesGracefully()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Throws(new IOException("Access denied"));

        var result = _service.CalculateStatistics();

        // Should not throw, stats should be zero
        Assert.Single(result.Libraries);
        Assert.Equal(0, result.Libraries[0].VideoSize);
    }

    [Fact]
    public void CalculateStatistics_UnauthorizedAccessInDirectory_ContinuesGracefully()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Throws(new UnauthorizedAccessException("Forbidden"));

        var result = _service.CalculateStatistics();

        Assert.Single(result.Libraries);
        Assert.Equal(0, result.Libraries[0].VideoSize);
    }

    [Fact]
    public void CalculateStatistics_MultipleLibraries_AggregatedCorrectly()
    {
        var moviePath = TestPath("media", "movies");
        var tvPath = TestPath("media", "tv");

        var movieFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [moviePath]
        };
        var tvFolder = new VirtualFolderInfo
        {
            Name = "TV Shows",
            CollectionType = CollectionTypeOptions.tvshows,
            Locations = [tvPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([movieFolder, tvFolder]);

        var movieFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.mkv"),
            Name = "Film.mkv",
            Length = 2_000_000_000,
            IsDirectory = false
        };

        var tvFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "tv", "Episode.mkv"),
            Name = "Episode.mkv",
            Length = 500_000_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(moviePath, false)).Returns([movieFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(moviePath, false)).Returns([]);
        _fileSystemMock.Setup(f => f.GetFiles(tvPath, false)).Returns([tvFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(tvPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(2, result.Libraries.Count);
        Assert.Single(result.Movies);
        Assert.Single(result.TvShows);
        Assert.Equal(2_000_000_000, result.TotalMovieVideoSize);
        Assert.Equal(500_000_000, result.TotalTvShowVideoSize);
    }

    [Fact]
    public void CalculateStatistics_HomeVideosLibrary_ClassifiedAsMovies()
    {
        var libraryPath = TestPath("media", "homevideos");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Home Videos",
            CollectionType = CollectionTypeOptions.homevideos,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var videoFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "homevideos", "vacation.mp4"),
            Name = "vacation.mp4",
            Length = 1_000_000_000,
            IsDirectory = false
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([videoFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Single(result.Movies);
        Assert.Empty(result.TvShows);
        Assert.Equal(1_000_000_000, result.TotalMovieVideoSize);
    }

    [Fact]
    public void CalculateStatistics_NullCollectionType_ClassifiedAsOther()
    {
        var libraryPath = TestPath("media", "misc");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Misc",
            CollectionType = null,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Single(result.Other);
        Assert.Empty(result.Movies);
        Assert.Empty(result.TvShows);
        Assert.Empty(result.Music);
    }

    [Fact]
    public void CalculateStatistics_MixedFileTypes_AllCategorizedCorrectly()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.mkv"), Name = "Film.mkv", Length = 1_000_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.srt"), Name = "Film.srt", Length = 50_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "poster.jpg"), Name = "poster.jpg", Length = 200_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.nfo"), Name = "Film.nfo", Length = 10_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "theme.mp3"), Name = "theme.mp3", Length = 5_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "readme.txt"), Name = "readme.txt", Length = 1_000, IsDirectory = false }
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(1_000_000_000, stats.VideoSize);
        Assert.Equal(1, stats.VideoFileCount);
        Assert.Equal(50_000, stats.SubtitleSize);
        Assert.Equal(1, stats.SubtitleFileCount);
        Assert.Equal(200_000, stats.ImageSize);
        Assert.Equal(1, stats.ImageFileCount);
        Assert.Equal(10_000, stats.NfoSize);
        Assert.Equal(1, stats.NfoFileCount);
        Assert.Equal(5_000_000, stats.AudioSize);
        Assert.Equal(1, stats.AudioFileCount);
        Assert.Equal(1_000, stats.OtherSize);
        Assert.Equal(1, stats.OtherFileCount);
    }

    [Fact]
    public void CalculateStatistics_TrickplayFolderCaseInsensitive_Detected()
    {
        var libraryPath = TestPath("media", "movies");
        var trickplayPath = TestPath("media", "movies", "Film.TRICKPLAY");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([]);

        var trickplayDir = new FileSystemMetadata
        {
            FullName = trickplayPath,
            Name = "Film.TRICKPLAY",
            IsDirectory = true
        };
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([trickplayDir]);

        var trickplayFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.TRICKPLAY", "001.jpg"),
            Name = "001.jpg",
            Length = 10_000,
            IsDirectory = false
        };
        _fileSystemMock.Setup(f => f.GetFiles(trickplayPath, false)).Returns([trickplayFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(trickplayPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(10_000, result.Libraries[0].TrickplaySize);
        Assert.Equal(1, result.Libraries[0].TrickplayFolderCount);
    }

    [Fact]
    public void CalculateStatistics_NestedTrickplayFolder_SizeIncludesSubdirectories()
    {
        var libraryPath = TestPath("media", "movies");
        var trickplayPath = TestPath("media", "movies", "Film.trickplay");
        var trickplaySubDir = TestPath("media", "movies", "Film.trickplay", "320");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([]);

        var trickplayDir = new FileSystemMetadata
        {
            FullName = trickplayPath,
            Name = "Film.trickplay",
            IsDirectory = true
        };
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([trickplayDir]);

        // Trickplay root files
        var rootFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.trickplay", "index.bif"),
            Name = "index.bif",
            Length = 5_000,
            IsDirectory = false
        };
        _fileSystemMock.Setup(f => f.GetFiles(trickplayPath, false)).Returns([rootFile]);

        // Trickplay subdirectory
        var subDir = new FileSystemMetadata
        {
            FullName = trickplaySubDir,
            Name = "320",
            IsDirectory = true
        };
        _fileSystemMock.Setup(f => f.GetDirectories(trickplayPath, false)).Returns([subDir]);

        var subFile = new FileSystemMetadata
        {
            FullName = TestPath("media", "movies", "Film.trickplay", "320", "001.jpg"),
            Name = "001.jpg",
            Length = 15_000,
            IsDirectory = false
        };
        _fileSystemMock.Setup(f => f.GetFiles(trickplaySubDir, false)).Returns([subFile]);
        _fileSystemMock.Setup(f => f.GetDirectories(trickplaySubDir, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal(20_000, result.Libraries[0].TrickplaySize);
    }

    [Fact]
    public void CalculateStatistics_LibraryName_PreservedInResult()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "My Movie Collection",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns([]);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();

        Assert.Equal("My Movie Collection", result.Libraries[0].LibraryName);
        Assert.Equal("movies", result.Libraries[0].CollectionType);
    }

    [Fact]
    public void LibraryStatistics_TotalSize_SumsAllCategories()
    {
        var stats = new LibraryStatistics
        {
            VideoSize = 1000,
            SubtitleSize = 200,
            ImageSize = 100,
            NfoSize = 50,
            AudioSize = 500,
            TrickplaySize = 300,
            OtherSize = 25
        };

        Assert.Equal(2175, stats.TotalSize);
    }

    // ===== Video Codec Parsing Tests =====

    [Theory]
    [InlineData("Movie.x265.mkv", "HEVC")]
    [InlineData("Movie.HEVC.mkv", "HEVC")]
    [InlineData("Movie.H.265.mkv", "HEVC")]
    [InlineData("Movie.h265.mkv", "HEVC")]
    [InlineData("Movie.x264.mkv", "H.264")]
    [InlineData("Movie.H.264.mkv", "H.264")]
    [InlineData("Movie.AVC.mkv", "H.264")]
    [InlineData("Movie.AV1.mkv", "AV1")]
    [InlineData("Movie.VP9.webm", "VP9")]
    [InlineData("Movie.XviD.avi", "XviD")]
    [InlineData("Movie.DivX.avi", "DivX")]
    [InlineData("Movie.MPEG2.mpg", "MPEG")]
    [InlineData("Movie.mkv", "Unknown")]
    public void ParseVideoCodec_DetectsCorrectCodec(string fileName, string expected)
    {
        var result = MediaStatisticsService.ParseVideoCodec(fileName);
        Assert.Equal(expected, result);
    }

    // ===== Resolution Parsing Tests =====

    [Theory]
    [InlineData("Movie.2160p.mkv", "4K")]
    [InlineData("Movie.4K.mkv", "4K")]
    [InlineData("Movie.UHD.mkv", "4K")]
    [InlineData("Movie.1080p.mkv", "1080p")]
    [InlineData("Movie.1080i.mkv", "1080p")]
    [InlineData("Movie.720p.mkv", "720p")]
    [InlineData("Movie.480p.mkv", "480p")]
    [InlineData("Movie.SD.mkv", "480p")]
    [InlineData("Movie.576p.mkv", "576p")]
    [InlineData("Movie.mkv", "Unknown")]
    public void ParseResolution_DetectsCorrectResolution(string fileName, string expected)
    {
        var result = MediaStatisticsService.ParseResolution(fileName);
        Assert.Equal(expected, result);
    }

    // ===== Audio Codec Parsing Tests =====

    [Theory]
    [InlineData("Song.FLAC.mp3", ".mp3", "FLAC")]
    [InlineData("Song.AAC.m4a", ".m4a", "AAC")]
    [InlineData("Song.Opus.ogg", ".ogg", "Opus")]
    [InlineData("Song.DTS.mkv", ".mkv", "DTS")]
    [InlineData("Song.AC3.mkv", ".mkv", "AC3")]
    [InlineData("Song.EAC3.mkv", ".mkv", "EAC3")]
    [InlineData("Song.TrueHD.mkv", ".mkv", "TrueHD")]
    [InlineData("Song.Vorbis.ogg", ".ogg", "Vorbis")]
    [InlineData("Song.ALAC.m4a", ".m4a", "ALAC")]
    [InlineData("Song.PCM.wav", ".wav", "PCM")]
    public void ParseAudioCodec_FromFilenameTag_DetectsCorrectCodec(string fileName, string ext, string expected)
    {
        var result = MediaStatisticsService.ParseAudioCodec(fileName, ext);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Song.flac", ".flac", "FLAC")]
    [InlineData("Track.mp3", ".mp3", "MP3")]
    [InlineData("Music.ogg", ".ogg", "Vorbis")]
    [InlineData("Sound.opus", ".opus", "Opus")]
    [InlineData("Audio.wav", ".wav", "WAV")]
    [InlineData("Music.wma", ".wma", "WMA")]
    [InlineData("Song.m4a", ".m4a", "AAC")]
    [InlineData("Music.aac", ".aac", "AAC")]
    [InlineData("Lossless.ape", ".ape", "APE")]
    [InlineData("Music.wv", ".wv", "WavPack")]
    [InlineData("HiRes.dsf", ".dsf", "DSD")]
    [InlineData("HiRes.dff", ".dff", "DSD")]
    public void ParseAudioCodec_FromExtension_DetectsCorrectCodec(string fileName, string ext, string expected)
    {
        var result = MediaStatisticsService.ParseAudioCodec(fileName, ext);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseAudioCodec_UnknownExtension_ReturnsUnknown()
    {
        var result = MediaStatisticsService.ParseAudioCodec("file.xyz", ".xyz");
        Assert.Equal("Unknown", result);
    }

    // ===== Container Format Tracking Tests =====

    [Fact]
    public void CalculateStatistics_VideoFiles_TracksContainerFormats()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film1.mkv"), Name = "Film1.mkv", Length = 1000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film2.mkv"), Name = "Film2.mkv", Length = 2000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film3.mp4"), Name = "Film3.mp4", Length = 3000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(2, stats.ContainerFormats["MKV"]);
        Assert.Equal(1, stats.ContainerFormats["MP4"]);
        Assert.Equal(3000, stats.ContainerSizes["MKV"]);
        Assert.Equal(3000, stats.ContainerSizes["MP4"]);
    }

    // ===== Audio Codec Tracking in Statistics =====

    [Fact]
    public void CalculateStatistics_AudioFiles_TracksMusicAudioCodecs()
    {
        var libraryPath = TestPath("media", "music");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Music",
            CollectionType = CollectionTypeOptions.music,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "music", "Song1.flac"), Name = "Song1.flac", Length = 30_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "music", "Song2.flac"), Name = "Song2.flac", Length = 25_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "music", "Song3.mp3"), Name = "Song3.mp3", Length = 5_000_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(2, stats.MusicAudioCodecs["FLAC"]);
        Assert.Equal(1, stats.MusicAudioCodecs["MP3"]);
        Assert.Equal(55_000_000, stats.MusicAudioCodecSizes["FLAC"]);
        Assert.Equal(5_000_000, stats.MusicAudioCodecSizes["MP3"]);
    }

    // ===== Health Check Tests =====

    [Fact]
    public void CalculateStatistics_VideoWithoutSubtitles_CountedInHealthCheck()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.mkv"), Name = "Film.mkv", Length = 1_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "poster.jpg"), Name = "poster.jpg", Length = 100_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.nfo"), Name = "Film.nfo", Length = 5_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(1, stats.VideosWithoutSubtitles);
        Assert.Equal(0, stats.VideosWithoutImages);
        Assert.Equal(0, stats.VideosWithoutNfo);
    }

    [Fact]
    public void CalculateStatistics_VideoWithAllMetadata_NoHealthWarnings()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.mkv"), Name = "Film.mkv", Length = 1_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.srt"), Name = "Film.srt", Length = 50_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "poster.jpg"), Name = "poster.jpg", Length = 100_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.nfo"), Name = "Film.nfo", Length = 5_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(0, stats.VideosWithoutSubtitles);
        Assert.Equal(0, stats.VideosWithoutImages);
        Assert.Equal(0, stats.VideosWithoutNfo);
        Assert.Equal(0, stats.OrphanedMetadataDirectories);
    }

    [Fact]
    public void CalculateStatistics_OrphanedMetadata_DetectedCorrectly()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        // Directory with subtitles but no video
        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.srt"), Name = "Film.srt", Length = 50_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.nfo"), Name = "Film.nfo", Length = 5_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(1, stats.OrphanedMetadataDirectories);
    }

    // ===== Audio Codec Tracking from Video Filenames =====

    [Fact]
    public void CalculateStatistics_VideoWithAudioCodecInFilename_TracksVideoAudioCodecs()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.x265.DTS.1080p.mkv"), Name = "Film.x265.DTS.1080p.mkv", Length = 2_000_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film2.x264.AC3.720p.mkv"), Name = "Film2.x264.AC3.720p.mkv", Length = 1_500_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film3.x265.DTS.4K.mkv"), Name = "Film3.x265.DTS.4K.mkv", Length = 5_000_000_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(2, stats.VideoAudioCodecs["DTS"]);
        Assert.Equal(1, stats.VideoAudioCodecs["AC3"]);
        Assert.Equal(7_000_000_000, stats.VideoAudioCodecSizes["DTS"]);
        Assert.Equal(1_500_000_000, stats.VideoAudioCodecSizes["AC3"]);
    }

    [Fact]
    public void CalculateStatistics_VideoWithoutAudioCodecInFilename_DoesNotTrackUnknown()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.mkv"), Name = "Film.mkv", Length = 1_000_000_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Empty(stats.VideoAudioCodecs);
    }

    [Fact]
    public void CalculateStatistics_OggFile_ClassifiedAsAudio()
    {
        var libraryPath = TestPath("media", "music");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Music",
            CollectionType = CollectionTypeOptions.music,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "music", "Song.ogg"), Name = "Song.ogg", Length = 5_000_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(1, stats.AudioFileCount);
        Assert.Equal(0, stats.VideoFileCount);
        Assert.Equal(5_000_000, stats.AudioSize);
        Assert.Equal(1, stats.MusicAudioCodecs["Vorbis"]);
    }

    // ===== Resolution & Codec Tracking in Statistics =====

    [Fact]
    public void CalculateStatistics_VideoWithCodecInFilename_TracksVideoCodecs()
    {
        var libraryPath = TestPath("media", "movies");

        var virtualFolder = new VirtualFolderInfo
        {
            Name = "Movies",
            CollectionType = CollectionTypeOptions.movies,
            Locations = [libraryPath]
        };
        _libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([virtualFolder]);

        var files = new[]
        {
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film.x265.1080p.mkv"), Name = "Film.x265.1080p.mkv", Length = 2_000_000_000, IsDirectory = false },
            new FileSystemMetadata { FullName = TestPath("media", "movies", "Film2.x264.720p.mkv"), Name = "Film2.x264.720p.mkv", Length = 1_500_000_000, IsDirectory = false },
        };

        _fileSystemMock.Setup(f => f.GetFiles(libraryPath, false)).Returns(files);
        _fileSystemMock.Setup(f => f.GetDirectories(libraryPath, false)).Returns([]);

        var result = _service.CalculateStatistics();
        var stats = result.Libraries[0];

        Assert.Equal(1, stats.VideoCodecs["HEVC"]);
        Assert.Equal(1, stats.VideoCodecs["H.264"]);
        Assert.Equal(1, stats.Resolutions["1080p"]);
        Assert.Equal(1, stats.Resolutions["720p"]);
    }

    // ===== MediaStatisticsResult Aggregation Tests =====

    [Fact]
    public void MediaStatisticsResult_Aggregation_SumsCorrectly()
    {
        var result = new MediaStatisticsResult();

        var movieLib = new LibraryStatistics
        {
            LibraryName = "Movies",
            CollectionType = "movies",
            VideoSize = 100,
            VideoFileCount = 5,
            AudioSize = 10,
            AudioFileCount = 2,
        };

        var tvLib = new LibraryStatistics
        {
            LibraryName = "TV",
            CollectionType = "tvshows",
            VideoSize = 200,
            VideoFileCount = 10,
        };

        var musicLib = new LibraryStatistics
        {
            LibraryName = "Music",
            CollectionType = "music",
            AudioSize = 50,
            AudioFileCount = 20,
        };

        result.Libraries.Add(movieLib);
        result.Libraries.Add(tvLib);
        result.Libraries.Add(musicLib);
        result.Movies.Add(movieLib);
        result.TvShows.Add(tvLib);
        result.Music.Add(musicLib);

        Assert.Equal(100, result.TotalMovieVideoSize);
        Assert.Equal(200, result.TotalTvShowVideoSize);
        Assert.Equal(50, result.TotalMusicAudioSize);
        Assert.Equal(15, result.TotalVideoFileCount);
        Assert.Equal(22, result.TotalAudioFileCount);
    }

    // ===== StatisticsSnapshot Tests =====

    [Fact]
    public void StatisticsSnapshot_FromResult_CapturesCorrectValues()
    {
        var result = new MediaStatisticsResult();
        result.ScanTimestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var lib1 = new LibraryStatistics
        {
            LibraryName = "Movies",
            CollectionType = "movies",
            VideoSize = 5000,
            VideoFileCount = 10,
            SubtitleSize = 100,
            ImageSize = 200,
            NfoSize = 50,
            TrickplaySize = 300,
        };

        var lib2 = new LibraryStatistics
        {
            LibraryName = "Music",
            CollectionType = "music",
            AudioSize = 1000,
            AudioFileCount = 50,
        };

        result.Libraries.Add(lib1);
        result.Libraries.Add(lib2);
        result.Movies.Add(lib1);
        result.Music.Add(lib2);

        var snapshot = StatisticsSnapshot.FromResult(result);

        Assert.Equal(result.ScanTimestamp, snapshot.Timestamp);
        Assert.Equal(10, snapshot.TotalVideoFileCount);
        Assert.Equal(50, snapshot.TotalAudioFileCount);
        Assert.Equal(5000, snapshot.TotalMovieVideoSize);
        Assert.Equal(0, snapshot.TotalTvShowVideoSize);
        Assert.Equal(1000, snapshot.TotalMusicAudioSize);
        Assert.Equal(300, snapshot.TotalTrickplaySize);
        Assert.Equal(100, snapshot.TotalSubtitleSize);
        Assert.Equal(200, snapshot.TotalImageSize);
        Assert.Equal(50, snapshot.TotalNfoSize);
        Assert.Equal(5650 + 1000, snapshot.TotalSize);
        Assert.Equal(2, snapshot.LibrarySizes.Count);
        Assert.Equal(5650, snapshot.LibrarySizes["Movies"]);
        Assert.Equal(1000, snapshot.LibrarySizes["Music"]);
    }

    [Fact]
    public void StatisticsSnapshot_FromResult_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => StatisticsSnapshot.FromResult(null!));
    }

    // ===== PathValidator Tests =====

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void PathValidator_IsSafePath_RejectsEmptyInput(string? path, bool expected)
    {
        Assert.Equal(expected, PathValidator.IsSafePath(path, "/base"));
    }

    [Fact]
    public void PathValidator_IsSafePath_RejectsTraversal()
    {
        Assert.False(PathValidator.IsSafePath("/base/../etc/passwd", "/base"));
        Assert.False(PathValidator.IsSafePath("/base/sub/../../etc", "/base"));
    }

    [Fact]
    public void PathValidator_IsSafePath_RejectsNullBytes()
    {
        Assert.False(PathValidator.IsSafePath("/base/file\0.txt", "/base"));
    }

    [Fact]
    public void PathValidator_SanitizeFileName_RemovesDirectoryComponents()
    {
        var result = PathValidator.SanitizeFileName("../../etc/passwd");
        Assert.Equal("passwd", result);
    }

    [Fact]
    public void PathValidator_SanitizeFileName_HandlesEmptyInput()
    {
        Assert.Equal("export", PathValidator.SanitizeFileName(""));
        Assert.Equal("export", PathValidator.SanitizeFileName("   "));
    }

    [Fact]
    public void PathValidator_SanitizeFileName_PreservesValidName()
    {
        Assert.Equal("report.csv", PathValidator.SanitizeFileName("report.csv"));
    }

    // ===== MediaExtensions Codec Mapping Tests =====

    [Fact]
    public void MediaExtensions_AudioExtensionToCodec_ContainsAllAudioExtensions()
    {
        // Every audio extension should have a codec mapping
        foreach (var ext in MediaExtensions.AudioExtensions)
        {
            Assert.True(
                MediaExtensions.AudioExtensionToCodec.ContainsKey(ext),
                $"Audio extension '{ext}' has no codec mapping in AudioExtensionToCodec");
        }
    }

    [Fact]
    public void MediaExtensions_AudioExtensionToCodec_ReturnsCorrectCodecs()
    {
        Assert.Equal("FLAC", MediaExtensions.AudioExtensionToCodec[".flac"]);
        Assert.Equal("MP3", MediaExtensions.AudioExtensionToCodec[".mp3"]);
        Assert.Equal("AAC", MediaExtensions.AudioExtensionToCodec[".aac"]);
        Assert.Equal("AAC", MediaExtensions.AudioExtensionToCodec[".m4a"]);
        Assert.Equal("Opus", MediaExtensions.AudioExtensionToCodec[".opus"]);
        Assert.Equal("Vorbis", MediaExtensions.AudioExtensionToCodec[".ogg"]);
        Assert.Equal("DSD", MediaExtensions.AudioExtensionToCodec[".dsf"]);
    }

    [Fact]
    public void MediaExtensions_AudioExtensionToCodec_IsCaseInsensitive()
    {
        Assert.Equal("FLAC", MediaExtensions.AudioExtensionToCodec[".FLAC"]);
        Assert.Equal("MP3", MediaExtensions.AudioExtensionToCodec[".Mp3"]);
    }
}
