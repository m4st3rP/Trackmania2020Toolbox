using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Trackmania2020Toolbox;

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
    public void GetItems_ShouldReturnEmpty_WhenDirectoryDoesNotExist()
    {
        _fsMock.Setup(f => f.DirectoryExists("invalid")).Returns(false);
        var result = _service.GetItems("invalid", "", false);
        Assert.Empty(result);
    }

    [Fact]
    public void GetItems_ShouldReturnDirsAndFiles()
    {
        _fsMock.Setup(f => f.DirectoryExists("root")).Returns(true);
        _fsMock.Setup(f => f.GetDirectories("root")).Returns(new[] { "root/dir1", "root/dir2" });
        _fsMock.Setup(f => f.GetFiles("root", "*.Map.Gbx", SearchOption.TopDirectoryOnly)).Returns(new[] { "root/map1.Map.Gbx" });

        var result = _service.GetItems("root", "", false).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.DisplayName == "dir1" && r.IsDirectory);
        Assert.Contains(result, r => r.DisplayName == "dir2" && r.IsDirectory);
        Assert.Contains(result, r => r.DisplayName == "map1.Map.Gbx" && !r.IsDirectory);
    }

    [Fact]
    public void GetItems_ShouldFilterBySearchTerm()
    {
        _fsMock.Setup(f => f.DirectoryExists("root")).Returns(true);
        _fsMock.Setup(f => f.GetDirectories("root")).Returns(new[] { "root/apple", "root/banana" });
        _fsMock.Setup(f => f.GetFiles("root", "*.Map.Gbx", SearchOption.TopDirectoryOnly)).Returns(new[] { "root/apricot.Map.Gbx" });

        var result = _service.GetItems("root", "app", false).ToList();

        Assert.Single(result);
        Assert.Equal("apple", result[0].DisplayName);
    }

    [Fact]
    public void GetItems_ShouldSortCorrectly()
    {
        _fsMock.Setup(f => f.DirectoryExists("root")).Returns(true);
        _fsMock.Setup(f => f.GetDirectories("root")).Returns(new[] { "root/B", "root/A" });
        _fsMock.Setup(f => f.GetFiles("root", "*.Map.Gbx", SearchOption.TopDirectoryOnly)).Returns(new[] { "root/C.Map.Gbx" });

        // Ascending
        var resultAsc = _service.GetItems("root", "", false).ToList();
        Assert.Equal("A", resultAsc[0].DisplayName);
        Assert.Equal("B", resultAsc[1].DisplayName);
        Assert.Equal("C.Map.Gbx", resultAsc[2].DisplayName);

        // Descending
        var resultDesc = _service.GetItems("root", "", true).ToList();
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
        _service = new RealConfigService("/test/config.toml", _fsMock.Object);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldReturnDefaults_WhenFileNotFound()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        var config = await _service.LoadConfigAsync();

        Assert.NotNull(config);
        Assert.Equal(Config.Default.Fixer.FolderPath, config.Fixer.FolderPath);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldLoadCorrectValues()
    {
        var toml = "game_path = \"/path/tm.exe\"\nbrowser_folder = \"/maps\"\ndouble_click_to_play = false\nenter_to_play = false\nplay_after_download = true";
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllLines(It.IsAny<string>())).Returns(toml.Split('\n'));

        var config = await _service.LoadConfigAsync();

        Assert.Equal("/path/tm.exe", config.Desktop.GamePath);
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

        var config = Config.Default with {
            Desktop = new DesktopConfig("/path/tm.exe", "/maps", true, false, true)
        };

        _service.SaveConfig(config);

        Assert.Contains("game_path = \"/path/tm.exe\"", capturedToml);
        Assert.Contains("browser_folder = \"/maps\"", capturedToml);
        Assert.Contains("double_click_to_play = true", capturedToml);
        Assert.Contains("enter_to_play = false", capturedToml);
        Assert.Contains("play_after_download = true", capturedToml);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldHandleCorruptedFileGracefully()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllLines(It.IsAny<string>())).Returns(new[] { "invalid toml content" });

        var config = await _service.LoadConfigAsync();

        Assert.NotNull(config);
        Assert.True(config.Desktop.DoubleClickToPlay); // Default
    }
}
