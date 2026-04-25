using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using Xunit;
using Trackmania2020Toolbox;
using Tomlyn;
using Tomlyn.Model;

namespace Trackmania2020Toolbox.Tests;

public class BrowserServiceTests
{
    private readonly Mock<IFileSystem> _fsMock = new();
    private readonly RealBrowserService _service;

    public BrowserServiceTests()
    {
        _service = new RealBrowserService(_fsMock.Object);
    }

    [Fact]
    public void GetBrowserItems_ShouldReturnEmpty_WhenDirectoryDoesNotExist()
    {
        _fsMock.Setup(f => f.DirectoryExists("invalid")).Returns(false);
        var result = _service.GetBrowserItems("invalid", "", false);
        Assert.Empty(result);
    }

    [Fact]
    public void GetBrowserItems_ShouldReturnDirsAndFiles()
    {
        _fsMock.Setup(f => f.DirectoryExists("root")).Returns(true);
        _fsMock.Setup(f => f.GetDirectories("root")).Returns(new[] { "root/dir1", "root/dir2" });
        _fsMock.Setup(f => f.GetFiles("root", "*.Map.Gbx", SearchOption.TopDirectoryOnly)).Returns(new[] { "root/map1.Map.Gbx" });

        var result = _service.GetBrowserItems("root", "", false).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.DisplayName == "dir1" && r.IsDirectory);
        Assert.Contains(result, r => r.DisplayName == "dir2" && r.IsDirectory);
        Assert.Contains(result, r => r.DisplayName == "map1.Map.Gbx" && !r.IsDirectory);
    }

    [Fact]
    public void GetBrowserItems_ShouldFilterBySearchTerm()
    {
        _fsMock.Setup(f => f.DirectoryExists("root")).Returns(true);
        _fsMock.Setup(f => f.GetDirectories("root")).Returns(new[] { "root/apple", "root/banana" });
        _fsMock.Setup(f => f.GetFiles("root", "*.Map.Gbx", SearchOption.TopDirectoryOnly)).Returns(new[] { "root/apricot.Map.Gbx" });

        var result = _service.GetBrowserItems("root", "app", false).ToList();

        // apricot.Map.Gbx contains "ap", apple contains "app"
        // Wait, "app" is the filter. "apple" contains "app". "apricot.Map.Gbx" DOES NOT contain "app".
        // Ah! That's why it was 1. apricot != app.
        Assert.Single(result);
        Assert.Equal("apple", result[0].DisplayName);
    }

    [Fact]
    public void GetBrowserItems_ShouldSortCorrectly()
    {
        _fsMock.Setup(f => f.DirectoryExists("root")).Returns(true);
        _fsMock.Setup(f => f.GetDirectories("root")).Returns(new[] { "root/B", "root/A" });
        _fsMock.Setup(f => f.GetFiles("root", "*.Map.Gbx", SearchOption.TopDirectoryOnly)).Returns(new[] { "root/C.Map.Gbx" });

        // Ascending
        var resultAsc = _service.GetBrowserItems("root", "", false).ToList();
        Assert.Equal("A", resultAsc[0].DisplayName);
        Assert.Equal("B", resultAsc[1].DisplayName);
        Assert.Equal("C.Map.Gbx", resultAsc[2].DisplayName);

        // Descending
        var resultDesc = _service.GetBrowserItems("root", "", true).ToList();
        Assert.Equal("B", resultDesc[0].DisplayName);
        Assert.Equal("A", resultDesc[1].DisplayName);
        Assert.Equal("C.Map.Gbx", resultDesc[2].DisplayName);
    }
}

public class ConfigServiceTests
{
    private readonly Mock<IFileSystem> _fsMock = new();
    private readonly RealConfigService _service;

    public ConfigServiceTests()
    {
        _service = new RealConfigService(_fsMock.Object);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldReturnDefaults_WhenFileNotFound()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        var config = await _service.LoadConfigAsync("/test");

        Assert.NotNull(config);
        Assert.Null(config.App.SetGamePath);
        Assert.True(config.Desktop.DoubleClickToPlay);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldLoadCorrectValues()
    {
        var toml = "[App]\nSetGamePath = \"/path/tm.exe\"\n[Desktop]\nBrowserFolder = \"/maps\"\nDoubleClickToPlay = false\nEnterToPlay = false\nPlayAfterDownload = true";
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(toml);

        var config = await _service.LoadConfigAsync("/test");

        Assert.Equal("/path/tm.exe", config.App.SetGamePath);
        Assert.Equal("/maps", config.Desktop.BrowserFolder);
        Assert.False(config.Desktop.DoubleClickToPlay);
        Assert.False(config.Desktop.EnterToPlay);
        Assert.True(config.Desktop.PlayAfterDownload);
    }

    [Fact]
    public async Task SaveConfigAsync_ShouldWriteCorrectToml()
    {
        string capturedToml = "";
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(Task.CompletedTask)
               .Callback<string, string>((path, content) => capturedToml = content);

        var config = Config.Default;
        config.App.SetGamePath = "/path/tm.exe";
        config.Desktop.BrowserFolder = "/maps";
        config.Desktop.DoubleClickToPlay = true;
        config.Desktop.EnterToPlay = false;
        config.Desktop.PlayAfterDownload = true;

        await _service.SaveConfigAsync("/test", config);

        Assert.Contains("SetGamePath = \"/path/tm.exe\"", capturedToml);
        Assert.Contains("BrowserFolder = \"/maps\"", capturedToml);
        Assert.Contains("DoubleClickToPlay = true", capturedToml);
        Assert.Contains("EnterToPlay = false", capturedToml);
        Assert.Contains("PlayAfterDownload = true", capturedToml);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldHandleCorruptedFileGracefully()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync("invalid toml content");

        var config = await _service.LoadConfigAsync("/test");

        Assert.NotNull(config);
        Assert.True(config.Desktop.DoubleClickToPlay); // Default
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldHandlePartialFileGracefully()
    {
        // Missing [Desktop] and [Downloader] sections
        var toml = "[App]\nSetGamePath = \"/path/tm.exe\"";
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(toml);

        var config = await _service.LoadConfigAsync("/test");

        Assert.Equal("/path/tm.exe", config.App.SetGamePath);
        Assert.NotNull(config.Desktop);
        Assert.True(config.Desktop.DoubleClickToPlay); // Default from constructor
        Assert.Equal(1000, config.Downloader.DownloadDelayMs); // Default from constructor
    }

    [Fact]
    public async Task SaveConfigAsync_ShouldPersistDownloadDelay()
    {
        string capturedToml = "";
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(Task.CompletedTask)
               .Callback<string, string>((path, content) => capturedToml = content);

        var config = Config.Default;
        config.Downloader.DownloadDelayMs = 500;

        await _service.SaveConfigAsync("/test", config);

        Assert.Contains("DownloadDelayMs = 500", capturedToml);
    }
}
