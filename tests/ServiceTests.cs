using Moq;
using Tomlyn;
using Trackmania2020Toolbox;
using Xunit;

namespace Trackmania2020Toolbox.Tests;

public class ConfigServiceTests
{
    private readonly Mock<IFileSystem> _fsMock = new();
    private readonly Mock<IConsole> _consoleMock = new();
    private readonly RealConfigService _service;

    public ConfigServiceTests()
    {
        _service = new RealConfigService(_fsMock.Object, _consoleMock.Object);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldReturnDefault_WhenFileDoesNotExist()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        var config = await _service.LoadConfigAsync("/test");
        Assert.NotNull(config);
        Assert.Equal(Config.Default.Downloader.DownloadDelayMs, config.Downloader.DownloadDelayMs);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldReturnConfig_WhenFileExists()
    {
        var config = Config.Default;
        config.Downloader.DownloadDelayMs = 500;
        var toml = TomlSerializer.Serialize(config, ToolboxConfigContext.Default.Config);

        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(toml);

        var loaded = await _service.LoadConfigAsync("/test");
        Assert.Equal(500, loaded.Downloader.DownloadDelayMs);
    }

    [Fact]
    public void LoadConfig_ShouldReturnDefault_WhenFileDoesNotExist()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        var config = _service.LoadConfig("/test");
        Assert.NotNull(config);
        Assert.Equal(Config.Default.Downloader.DownloadDelayMs, config.Downloader.DownloadDelayMs);
    }

    [Fact]
    public void LoadConfig_ShouldReturnConfig_WhenFileExists()
    {
        var config = Config.Default;
        config.Downloader.DownloadDelayMs = 500;
        var toml = TomlSerializer.Serialize(config, ToolboxConfigContext.Default.Config);

        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllText(It.IsAny<string>())).Returns(toml);

        var loaded = _service.LoadConfig("/test");
        Assert.Equal(500, loaded.Downloader.DownloadDelayMs);
    }

    [Fact]
    public async Task LoadConfigAsync_ShouldFallbackToDefault_OnCorruptedFile()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("invalid = [toml");

        var config = await _service.LoadConfigAsync("/test");
        Assert.NotNull(config);
        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("ERROR"))), Times.AtLeastOnce);
    }

    [Fact]
    public void LoadConfig_ShouldFallbackToDefault_OnCorruptedFile()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("invalid = [toml");

        var config = _service.LoadConfig("/test");
        Assert.NotNull(config);
        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("ERROR"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SaveConfigAsync_ShouldWriteCorrectToml()
    {
        string? savedContent = null;
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((p, c, t) => savedContent = c)
            .Returns(Task.CompletedTask);

        var config = Config.Default;
        config.Downloader.DownloadDelayMs = 1234;

        await _service.SaveConfigAsync("/test", config);

        Assert.NotNull(savedContent);
        Assert.Contains("DownloadDelayMs = 1234", savedContent);
    }

    [Fact]
    public async Task SaveConfigAsync_ShouldPersistDownloadDelay()
    {
        string? savedContent = null;
        _fsMock.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((p, c, t) => savedContent = c)
            .Returns(Task.CompletedTask);

        var config = Config.Default;
        config.Downloader.DownloadDelayMs = 2000;

        await _service.SaveConfigAsync("/test", config);

        _fsMock.Verify(f => f.WriteAllTextAsync(It.Is<string>(p => p.EndsWith("config.toml")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("DownloadDelayMs = 2000", savedContent);
    }

    [Fact]
    public async Task ConfigService_ShouldBeThreadSafe()
    {
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_service.LoadConfigAsync("/test"));
            tasks.Add(_service.SaveConfigAsync("/test", Config.Default));
        }

        await Task.WhenAll(tasks);

        // Ensure no exceptions occurred and internal state is fine
        _fsMock.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }
}
