using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Trackmania2020Toolbox;
using ManiaAPI.TrackmaniaIO;
using TmEssentials;

namespace Trackmania2020Toolbox.Tests;

public class ComponentTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ITrackmaniaApi> _apiMock = new();
    private readonly Mock<INetworkService> _netMock = new();
    private readonly Mock<IConsole> _consoleMock = new();
    private readonly Mock<IDateTime> _dateTimeMock = new();
    private readonly Mock<IMapFixer> _fixerMock = new();
    private readonly RealFileSystem _fs = new();
    private readonly ToolboxApp _app;

    public ComponentTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TM2020ToolboxTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _app = new ToolboxApp(
            _apiMock.Object,
            _fs,
            _netMock.Object,
            _fixerMock.Object, // Mocked fixer for component tests to avoid Gbx.Parse crashes
            _consoleMock.Object,
            _dateTimeMock.Object,
            _tempDir
        );
        // Override the private _defaultMapsFolder for testing
        var field = typeof(ToolboxApp).GetField("_defaultMapsFolder", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_app, _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadAndFixFlow_ShouldCreateFilesOnDisk()
    {
        // 1. Setup mocks
        var campaignItem = new Mock<ICampaignItem>();
        campaignItem.Setup(c => c.Id).Returns(1);
        campaignItem.Setup(c => c.Name).Returns("Winter 2024");

        var collection = new Mock<ICampaignCollection>();
        collection.Setup(c => c.Campaigns).Returns(new[] { campaignItem.Object });
        collection.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(0)).ReturnsAsync(collection.Object);

        var map = new Mock<IMap>();
        map.Setup(m => m.Name).Returns("Map 01");
        map.Setup(m => m.FileUrl).Returns("http://fake/map.gbx");
        map.Setup(m => m.FileName).Returns("Winter2024_01.Map.Gbx");

        var campaign = new Mock<ICampaign>();
        campaign.Setup(c => c.Playlist).Returns(new[] { map.Object });
        campaign.Setup(c => c.Name).Returns("Winter 2024");
        _apiMock.Setup(a => a.GetSeasonalCampaignAsync(1)).ReturnsAsync(campaign.Object);

        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[100]);
        _fixerMock.Setup(f => f.ProcessFile(It.IsAny<string>(), It.IsAny<Config>())).Returns(true);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--seasonal", "Winter 2024", "--folder", _tempDir });

        // 2. Run
        await _app.RunAsync(config);

        // 3. Verify on disk
        var expectedDir = Path.Combine(_tempDir, "Seasonal", "2024 - 1 - Winter");
        Assert.True(Directory.Exists(expectedDir), $"Directory should exist: {expectedDir}");

        var files = Directory.GetFiles(expectedDir);
        Assert.Single(files);
        Assert.Contains("01 - Winter2024_01.Map.Gbx", Path.GetFileName(files[0]));
    }

    [Fact]
    public void BatchFixer_ShouldUpdateRealFilesOnDisk()
    {
        var mapPath = Path.Combine(_tempDir, "test.Map.Gbx");
        File.WriteAllBytes(mapPath, new byte[100]); // Dummy file

        _fixerMock.Setup(f => f.ProcessFile(It.IsAny<string>(), It.IsAny<Config>())).Returns(true);

        var config = new Config(
            new DownloaderConfig(null, null, null, null, null, null, null),
            new TmxConfig(null, null, null, null, "name", false, false),
            new FixerConfig(_tempDir, true, true, true, false),
            new AppConfig(false, true, false, null, new List<string>()),
            new DesktopConfig(_tempDir, true, true, false)
        );

        var processed = _app.RunBatchFixer(config);

        Assert.Single(processed);
        Assert.Equal(mapPath, processed[0]);
        _fixerMock.Verify(f => f.ProcessFile(mapPath, It.IsAny<Config>()), Times.Once);
    }
}
