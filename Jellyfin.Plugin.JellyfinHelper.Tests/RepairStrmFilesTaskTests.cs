using System;
using System.IO.Abstractions.TestingHelpers;
using Jellyfin.Plugin.JellyfinHelper.Configuration;
using Jellyfin.Plugin.JellyfinHelper.ScheduledTasks;
using Jellyfin.Plugin.JellyfinHelper.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests;

public class RepairStrmFilesTaskTests
{
    private static RepairStrmFilesTask CreateTask()
    {
        var loggerMock = new Mock<ILogger<RepairStrmFilesTask>>();
        var libraryManagerMock = new Mock<ILibraryManager>();
        var fileSystem = new MockFileSystem();
        var serviceLoggerMock = new Mock<ILogger<StrmRepairService>>();
        var service = new StrmRepairService(fileSystem, serviceLoggerMock.Object);
        return new RepairStrmFilesTask(loggerMock.Object, libraryManagerMock.Object, service);
    }

    [Fact]
    public void Name_ReturnsExpected()
    {
        var task = CreateTask();
        Assert.Equal("Repair broken .strm files", task.Name);
    }

    [Fact]
    public void Key_ReturnsExpected()
    {
        var task = CreateTask();
        Assert.Equal("RepairStrmFiles", task.Key);
    }

    [Fact]
    public void Category_ReturnsJellyfinHelper()
    {
        var task = CreateTask();
        Assert.Equal("JellyfinHelper", task.Category);
    }

    [Fact]
    public void GetDefaultTriggers_ReturnsDailyTrigger()
    {
        var task = CreateTask();

        var triggers = task.GetDefaultTriggers();

        var trigger = Assert.Single(triggers);
        Assert.Equal(TaskTriggerInfoType.DailyTrigger, trigger.Type);
        Assert.Equal(TimeSpan.FromHours(5).Ticks, trigger.TimeOfDayTicks);
    }
}