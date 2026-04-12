using Jellyfin.Plugin.JellyfinHelper.Api;
using Jellyfin.Plugin.JellyfinHelper.Services.Backup;
using Jellyfin.Plugin.JellyfinHelper.Services.PluginLog;
using Jellyfin.Plugin.JellyfinHelper.Services.Statistics;
using Jellyfin.Plugin.JellyfinHelper.Services.Timeline;
using Jellyfin.Plugin.JellyfinHelper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Api;

public class MediaStatisticsControllerBackupTests
{
    [Fact]
    public void ExportBackup_WhenPayloadIsLargeButWithinLimit_ReturnsFileAndLogsWarning()
    {
        PluginLogService.Clear();
        var tempDir = CreateTempDir();
        try
        {
            WriteBaselineJsonAtLeast(
                Path.Combine(tempDir, "jellyfin-helper-growth-baseline.json"),
                (int)BackupService.LargeBackupWarningThresholdBytes + (256 * 1024));

            var controller = CreateController(tempDir);

            var result = controller.ExportBackup();

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/json", fileResult.ContentType);

            var logs = PluginLogService.GetEntries(source: "API", limit: 20);
            Assert.Contains(logs, entry => entry.Level == "WARN" && entry.Message.Contains("Large backup export created", StringComparison.Ordinal));
        }
        finally
        {
            PluginLogService.Clear();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportBackup_WhenPayloadExceedsLimit_ReturnsBadRequestAndLogsWarning()
    {
        PluginLogService.Clear();
        var tempDir = CreateTempDir();
        try
        {
            WriteBaselineJsonAtLeast(
                Path.Combine(tempDir, "jellyfin-helper-growth-baseline.json"),
                (int)BackupService.MaxBackupSizeBytes + (256 * 1024));

            var controller = CreateController(tempDir);

            var result = controller.ExportBackup();

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var payloadJson = JsonSerializer.Serialize(badRequest.Value);
            Assert.Contains("Maximum size is 50 MB", payloadJson, StringComparison.Ordinal);

            var logs = PluginLogService.GetEntries(source: "API", limit: 20);
            Assert.Contains(logs, entry => entry.Level == "WARN" && entry.Message.Contains("Backup export rejected", StringComparison.Ordinal));
        }
        finally
        {
            PluginLogService.Clear();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImportBackup_WhenContentLengthExceedsLimit_ReturnsBadRequest()
    {
        PluginLogService.Clear();
        var tempDir = CreateTempDir();
        try
        {
            var controller = CreateControllerWithJsonBody(tempDir, "{}", contentLength: BackupService.MaxBackupSizeBytes + 1);

            var result = await controller.ImportBackupAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var payloadJson = JsonSerializer.Serialize(badRequest.Value);
            Assert.Contains("Maximum size is 50 MB", payloadJson, StringComparison.Ordinal);
        }
        finally
        {
            PluginLogService.Clear();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImportBackup_WhenContentLengthIsLargeButWithinLimit_LogsWarning()
    {
        PluginLogService.Clear();
        var tempDir = CreateTempDir();
        try
        {
            // Use a minimal valid backup JSON but declare a large Content-Length
            var controller = CreateControllerWithJsonBody(tempDir, "{}", contentLength: BackupService.LargeBackupWarningThresholdBytes);

            var result = await controller.ImportBackupAsync();

            // The body is only "{}" so deserialization will produce a default BackupData
            // which passes validation — we just want to verify the warning was logged
            var logs = PluginLogService.GetEntries(source: "API", limit: 20);
            Assert.Contains(logs, entry => entry.Level == "WARN" && entry.Message.Contains("Large backup import detected", StringComparison.Ordinal));
        }
        finally
        {
            PluginLogService.Clear();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImportBackup_WhenBodyIsEmpty_ReturnsBadRequest()
    {
        PluginLogService.Clear();
        var tempDir = CreateTempDir();
        try
        {
            var controller = CreateControllerWithJsonBody(tempDir, string.Empty);

            var result = await controller.ImportBackupAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var payloadJson = JsonSerializer.Serialize(badRequest.Value);
            Assert.Contains("No backup data provided", payloadJson, StringComparison.Ordinal);
        }
        finally
        {
            PluginLogService.Clear();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExportBackup_ThenImportBackup_RoundTripsSuccessfully()
    {
        PluginLogService.Clear();
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "jellyfin-helper-growth-timeline.json"),
                "{\"granularity\":\"monthly\",\"dataPoints\":[{\"date\":\"2024-06-01T00:00:00Z\",\"cumulativeSize\":1000,\"cumulativeFileCount\":1}]}",
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(tempDir, "jellyfin-helper-growth-baseline.json"),
                "{\"firstScanTimestamp\":\"2024-04-01T00:00:00Z\",\"directories\":{\"/media/movie-1\":{\"createdUtc\":\"2024-04-01T00:00:00Z\",\"size\":2000}}}",
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(tempDir, "jellyfin-helper-statistics-history.json"),
                "[{\"timestamp\":\"2024-05-01T00:00:00Z\",\"totalSize\":500}]",
                Encoding.UTF8);

            var exportController = CreateController(tempDir);
            var exportResult = exportController.ExportBackup();
            var exportFile = Assert.IsType<FileContentResult>(exportResult);
            var exportedJson = Encoding.UTF8.GetString(exportFile.FileContents);

            // Create a new controller with the exported JSON as the request body
            var importController = CreateControllerWithJsonBody(tempDir, exportedJson);

            var importResult = await importController.ImportBackupAsync();

            var okResult = Assert.IsType<OkObjectResult>(importResult);
            var payloadJson = JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("Backup imported successfully.", payloadJson, StringComparison.Ordinal);
            Assert.Contains("baselineRestored", payloadJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            PluginLogService.Clear();
            Directory.Delete(tempDir, true);
        }
    }

    private static MediaStatisticsController CreateController(string dataPath)
    {
        var libraryManagerMock = new Mock<ILibraryManager>();
        libraryManagerMock.Setup(m => m.GetVirtualFolders()).Returns([]);

        var fileSystemMock = new Mock<IFileSystem>();
        var appPathsMock = new Mock<IApplicationPaths>();
        appPathsMock.Setup(p => p.DataPath).Returns(dataPath);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var loggerMock = new Mock<ILogger<MediaStatisticsController>>();
        var serviceLoggerMock = new Mock<ILogger<MediaStatisticsService>>();
        var historyLoggerMock = new Mock<ILogger<StatisticsHistoryService>>();
        var growthTimelineLoggerMock = new Mock<ILogger<GrowthTimelineService>>();

        return new MediaStatisticsController(
            libraryManagerMock.Object,
            fileSystemMock.Object,
            appPathsMock.Object,
            httpClientFactoryMock.Object,
            cache,
            loggerMock.Object,
            serviceLoggerMock.Object,
            historyLoggerMock.Object,
            growthTimelineLoggerMock.Object);
    }

    private static MediaStatisticsController CreateControllerWithJsonBody(string dataPath, string jsonBody, long? contentLength = null)
    {
        var controller = CreateController(dataPath);

        var httpContext = new DefaultHttpContext();
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = contentLength ?? bodyBytes.Length;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        return controller;
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "jh-backup-api-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void WriteBaselineJsonAtLeast(string filePath, int targetBytes)
    {
        var sb = new StringBuilder();
        sb.Append("{\"firstScanTimestamp\":\"2024-01-01T00:00:00Z\",\"directories\":{");

        var first = true;
        var suffix = new string('x', 860);
        var index = 0;

        while (sb.Length < targetBytes)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append('"')
                .Append("/media/")
                .Append(index.ToString("D6", CultureInfo.InvariantCulture))
                .Append('-')
                .Append(suffix)
                .Append("\":{\"createdUtc\":\"2024-01-01T00:00:00Z\",\"size\":1}");
            index++;
        }

        sb.Append("}}");
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
}