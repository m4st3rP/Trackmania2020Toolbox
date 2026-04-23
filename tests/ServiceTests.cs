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
    public void LoadConfig_ShouldReturnDefaults_WhenFileNotFound()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        var config = _service.LoadConfig("/test");

        Assert.NotNull(config);
        Assert.Null(config.App.SetGamePath);
        Assert.True(config.Desktop.DoubleClickToPlay);
    }

    [Fact]
    public void LoadConfig_ShouldLoadCorrectValues()
    {
        var toml = "game_path = \"/path/tm.exe\"\nbrowser_folder = \"/maps\"\ndouble_click_to_play = false\nenter_to_play = false\nplay_after_download = true";
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllLines(It.IsAny<string>())).Returns(toml.Split('\n'));

        var config = _service.LoadConfig("/test");

        Assert.Equal("/path/tm.exe", config.App.SetGamePath);
        Assert.Equal("/maps", config.Desktop.BrowserFolder);
        Assert.False(config.Desktop.DoubleClickToPlay);
        Assert.False(config.Desktop.EnterToPlay);
        Assert.True(config.Desktop.PlayAfterDownload);
    }

    [Fact]
    public void SaveConfig_ShouldWriteCorrectToml()
    {
        string capturedToml = "";
        _fsMock.Setup(f => f.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
               .Callback<string, string>((path, content) => capturedToml = content);

        _service.SaveConfig("/test", "/path/tm.exe", "/maps", true, false, true);

        Assert.Contains("game_path = \"/path/tm.exe\"", capturedToml);
        Assert.Contains("browser_folder = \"/maps\"", capturedToml);
        Assert.Contains("double_click_to_play = true", capturedToml);
        Assert.Contains("enter_to_play = false", capturedToml);
        Assert.Contains("play_after_download = true", capturedToml);
    }

    [Fact]
    public void LoadConfig_ShouldHandleCorruptedFileGracefully()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllLines(It.IsAny<string>())).Returns(new[] { "invalid toml content" });

        var config = _service.LoadConfig("/test");

        Assert.NotNull(config);
        Assert.True(config.Desktop.DoubleClickToPlay); // Default
    }
}
