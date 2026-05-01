using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using Moq;
using Xunit;
using Trackmania2020Toolbox;
using ManiaAPI.TrackmaniaIO;
using ManiaAPI.TMX;
using TmEssentials;

namespace Trackmania2020Toolbox.Tests;

public class ToolboxAppExpandedTests
{
    private readonly Mock<ITrackmaniaApi> _apiMock = new();
    private readonly Mock<IFileSystem> _fsMock = new();
    private readonly Mock<INetworkService> _netMock = new();
    private readonly Mock<IMapFixer> _fixerMock = new();
    private readonly Mock<IConsole> _consoleMock = new();
    private readonly Mock<IDateTime> _dateTimeMock = new();
    private readonly ToolboxApp _app;

    public ToolboxAppExpandedTests()
    {
        _app = new ToolboxApp(
            _apiMock.Object,
            _fsMock.Object,
            _netMock.Object,
            _fixerMock.Object,
            _consoleMock.Object,
            _dateTimeMock.Object,
            "/test"
        );
    }

    [Fact]
    public async Task HandleWeeklyGrandsAsync_ShouldFetchAndDownload()
    {
        var campaignItem = new Mock<ICampaignItem>();
        campaignItem.Setup(c => c.Id).Returns(123);
        campaignItem.Setup(c => c.Name).Returns("Week Grand 65");

        var collection = new Mock<ICampaignCollection>();
        collection.Setup(c => c.Campaigns).Returns(new[] { campaignItem.Object });
        collection.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetWeeklyGrandCampaignsAsync(0)).ReturnsAsync(collection.Object);

        var map = new Mock<IMap>();
        map.Setup(m => m.Name).Returns("Map 1");
        map.Setup(m => m.FileUrl).Returns("http://url");

        var campaign = new Mock<ICampaign>();
        campaign.Setup(c => c.Playlist).Returns(new[] { map.Object });
        _apiMock.Setup(a => a.GetWeeklyGrandCampaignAsync(123)).ReturnsAsync(campaign.Object);

        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[10]);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--weekly-grands", "65" }, Config.Default);
        await _app.HandleWeeklyGrandsAsync("65", config);

        _apiMock.Verify(a => a.GetWeeklyGrandCampaignAsync(123), Times.Once);
        _fsMock.Verify(f => f.WriteAllBytesAsync(It.Is<string>(s => s.Contains("65 - ")), It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task HandleClubCampaignAsync_ShouldSearchAndDownloadByName()
    {
        var campaignItem = new Mock<ICampaignItem>();
        campaignItem.Setup(c => c.Id).Returns(456);
        campaignItem.Setup(c => c.ClubId).Returns(10);
        campaignItem.Setup(c => c.Name).Returns("Club Race 1");

        var collection = new Mock<ICampaignCollection>();
        collection.Setup(c => c.Campaigns).Returns(new[] { campaignItem.Object });
        collection.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetClubCampaignsAsync(0)).ReturnsAsync(collection.Object);

        var map = new Mock<IMap>();
        map.Setup(m => m.Name).Returns("Map 1");
        map.Setup(m => m.FileUrl).Returns("http://url");

        var campaign = new Mock<ICampaign>();
        campaign.Setup(c => c.Playlist).Returns(new[] { map.Object });
        campaign.Setup(c => c.ClubName).Returns("ClubName");
        campaign.Setup(c => c.Name).Returns("Club Race 1");
        _apiMock.Setup(a => a.GetClubCampaignAsync(10, 456)).ReturnsAsync(campaign.Object);

        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[10]);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--club-campaign", "Race" }, Config.Default);
        await _app.HandleClubCampaignAsync("Race", config);

        _apiMock.Verify(a => a.GetClubCampaignAsync(10, 456), Times.Once);
    }

    [Fact]
    public async Task HandleTmxPacksAsync_ShouldDownloadAllMapsInPack()
    {
        var pack = new Mock<ITmxMapPack>();
        pack.Setup(p => p.Id).Returns(1);
        pack.Setup(p => p.Name).Returns("Cool Pack");

        _apiMock.Setup(a => a.GetTmxMapPackAsync(1)).ReturnsAsync(pack.Object);

        var map1 = new Mock<ITmxMap>();
        map1.Setup(m => m.Id).Returns(101);
        map1.Setup(m => m.Name).Returns("M1");
        var map2 = new Mock<ITmxMap>();
        map2.Setup(m => m.Id).Returns(102);
        map2.Setup(m => m.Name).Returns("M2");

        _apiMock.Setup(a => a.GetTmxMapPackMapsAsync(1)).ReturnsAsync(new[] { map1.Object, map2.Object });
        _apiMock.Setup(a => a.GetTmxMapUrl(It.IsAny<int>())).Returns("http://tmx/gbx");

        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[10]);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--tmx-pack", "1" }, Config.Default);
        await _app.HandleTmxPacksAsync("1", config);

        _netMock.Verify(n => n.GetByteArrayAsync("http://tmx/gbx"), Times.Exactly(2));
        _fsMock.Verify(f => f.WriteAllBytesAsync(It.Is<string>(s => s.Contains("Cool Pack")), It.IsAny<byte[]>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleTmxRandomAsync_ShouldDownloadOneMap()
    {
        var map = new Mock<ITmxMap>();
        map.Setup(m => m.Id).Returns(777);
        map.Setup(m => m.Name).Returns("Lucky");

        _apiMock.Setup(a => a.GetRandomTmxMapAsync()).ReturnsAsync(map.Object);
        _apiMock.Setup(a => a.GetTmxMapAsync(777)).ReturnsAsync(map.Object);
        _apiMock.Setup(a => a.GetTmxMapUrl(777)).Returns("http://url");

        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[10]);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--tmx-random" }, Config.Default);
        await _app.HandleTmxRandomAsync(config);

        _apiMock.Verify(a => a.GetRandomTmxMapAsync(), Times.Once);
        _netMock.Verify(n => n.GetByteArrayAsync("http://url"), Times.Once);
    }

    [Fact]
    public async Task HandleTmxSearchAsync_ShouldHandleNoResults()
    {
        _apiMock.Setup(a => a.SearchTmxMapsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(Enumerable.Empty<ITmxMap>());

        var config = TrackmaniaCLI.ParseArguments(new[] { "--tmx-search", "nothing" }, Config.Default);
        await _app.HandleTmxSearchAsync("nothing", null, "name", false, config);

        _consoleMock.Verify(c => c.WriteLine("No TMX maps found."), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldCallAllHandlers()
    {
        _dateTimeMock.Setup(d => d.UtcNow).Returns(new DateTime(2024, 1, 1, 18, 0, 0));

        var config = TrackmaniaCLI.ParseArguments(new[] {
            "--weekly-shorts", "1",
            "--weekly-grands", "1",
            "--seasonal", "Winter 2024",
            "--totd", "2024.01.01"
        }, Config.Default);

        var collection = new Mock<ICampaignCollection>();
        collection.Setup(c => c.Campaigns).Returns(Enumerable.Empty<ICampaignItem>());
        collection.Setup(c => c.PageCount).Returns(0);

        _apiMock.Setup(a => a.GetWeeklyShortCampaignsAsync(It.IsAny<int>())).ReturnsAsync(collection.Object);
        _apiMock.Setup(a => a.GetWeeklyGrandCampaignsAsync(It.IsAny<int>())).ReturnsAsync(collection.Object);
        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(It.IsAny<int>())).ReturnsAsync(collection.Object);

        var totdCollection = new Mock<ITrackOfTheDayCollection>();
        totdCollection.Setup(c => c.Days).Returns(Enumerable.Empty<ITrackOfTheDayDay>());
        totdCollection.Setup(c => c.Year).Returns(2024);
        totdCollection.Setup(c => c.Month).Returns(1);
        _apiMock.Setup(a => a.GetTrackOfTheDaysAsync(It.IsAny<int>())).ReturnsAsync(totdCollection.Object);

        await _app.RunAsync(config);

        _apiMock.Verify(a => a.GetWeeklyShortCampaignsAsync(It.IsAny<int>()), Times.AtLeastOnce);
        _apiMock.Verify(a => a.GetWeeklyGrandCampaignsAsync(It.IsAny<int>()), Times.AtLeastOnce);
        _apiMock.Verify(a => a.GetSeasonalCampaignsAsync(It.IsAny<int>()), Times.AtLeastOnce);
        _apiMock.Verify(a => a.GetTrackOfTheDaysAsync(It.IsAny<int>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandleTmxMapsAsync_ShouldHandleNotFound()
    {
        _apiMock.Setup(a => a.GetTmxMapAsync(It.IsAny<int>())).ReturnsAsync((ITmxMap?)null);
        var config = TrackmaniaCLI.ParseArguments(new[] { "--tmx", "999" }, Config.Default);

        await _app.HandleTmxMapsAsync("999", config);

        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Could not find TMX map 999"))), Times.Once);
    }

    [Fact]
    public async Task HandleClubCampaignAsync_ShouldHandleNoMatches()
    {
        var collection = new Mock<ICampaignCollection>();
        collection.Setup(c => c.Campaigns).Returns(Enumerable.Empty<ICampaignItem>());
        collection.Setup(c => c.PageCount).Returns(1);
        _apiMock.Setup(a => a.GetClubCampaignsAsync(It.IsAny<int>())).ReturnsAsync(collection.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--club-campaign", "NonExistent" }, Config.Default);
        await _app.HandleClubCampaignAsync("NonExistent", config);

        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("No matching club campaigns found"))), Times.Once);
    }

    [Fact]
    public async Task HandleSeasonalAsync_ShouldHandleNoMatches()
    {
        var collection = new Mock<ICampaignCollection>();
        collection.Setup(c => c.Campaigns).Returns(Enumerable.Empty<ICampaignItem>());
        collection.Setup(c => c.PageCount).Returns(1);
        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(It.IsAny<int>())).ReturnsAsync(collection.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--seasonal", "NotFound" }, Config.Default);
        await _app.HandleSeasonalAsync("NotFound", config);

        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Could not find seasonal campaign matching 'NotFound'"))), Times.Once);
    }

    [Fact]
    public async Task DownloadAndFixMapsAsync_ShouldProcessMapDownloadRecords()
    {
        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[5]);
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var maps = new List<MapDownloadRecord>
        {
            new MapDownloadRecord("Map A", "FileA", "http://a", "P1-"),
            new MapDownloadRecord("Map B", null, "http://b", null)
        };

        var config = Config.Default;
        var result = await new MapDownloader(_fsMock.Object, _netMock.Object, _fixerMock.Object, _consoleMock.Object).DownloadAndFixMapsAsync(maps, "/test/download", config);

        Assert.Equal(2, result.Count);
        _fsMock.Verify(f => f.WriteAllBytesAsync(It.Is<string>(s => s.Contains("P1-FileA")), It.IsAny<byte[]>()), Times.Once);
        _fsMock.Verify(f => f.WriteAllBytesAsync(It.Is<string>(s => s.Contains("Map B")), It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task DownloadClubCampaign_ShouldSanitizeFolderName()
    {
        var campaignItem = new Mock<ICampaignItem>();
        campaignItem.Setup(c => c.Id).Returns(456);
        campaignItem.Setup(c => c.ClubId).Returns(10);
        campaignItem.Setup(c => c.Name).Returns("Club / Campaign?");

        var collection = new Mock<ICampaignCollection>();
        collection.Setup(c => c.Campaigns).Returns([campaignItem.Object]);
        collection.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetClubCampaignsAsync(0)).ReturnsAsync(collection.Object);

        var map = new Mock<IMap>();
        map.Setup(m => m.Name).Returns("Map 1");
        map.Setup(m => m.FileUrl).Returns("http://url");

        var campaign = new Mock<ICampaign>();
        campaign.Setup(c => c.Playlist).Returns([map.Object]);
        campaign.Setup(c => c.ClubName).Returns("Club: Name");
        campaign.Setup(c => c.Name).Returns("Club / Campaign?");
        _apiMock.Setup(a => a.GetClubCampaignAsync(10, 456)).ReturnsAsync(campaign.Object);

        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[10]);

        var config = TrackmaniaCLI.ParseArguments(["--club-campaign", "10/456"], Config.Default);
        await _app.HandleClubCampaignAsync("10/456", config);

        // "/" and ":" should be replaced by "_"
        _fsMock.Verify(f => f.DirectoryExists(It.Is<string>(s => s.Contains("Club_ Name") && s.Contains("Club _ Campaign_"))), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("comma,separated", "\"comma,separated\"")]
    [InlineData("quote\"inside", "\"quote\"\"inside\"")]
    [InlineData("newline\ninside", "\"newline\ninside\"")]
    public void EscapeCsv_ShouldHandleSpecialCharacters(string input, string expected)
    {
        var result = ToolboxApp.EscapeCsv(input);
        Assert.Equal(expected, result);
    }
}
