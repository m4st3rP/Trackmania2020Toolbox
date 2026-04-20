using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Moq;
using Xunit;
using Trackmania2020Toolbox;
using ManiaAPI.TrackmaniaIO;
using TmEssentials;
using System.Net.Http;

namespace Trackmania2020Toolbox.Tests;

public class ToolboxTests
{
    private readonly Mock<ITrackmaniaApi> _apiMock = new();
    private readonly Mock<IFileSystem> _fsMock = new();
    private readonly Mock<INetworkService> _netMock = new();
    private readonly Mock<IMapFixer> _fixerMock = new();
    private readonly Mock<IConsole> _consoleMock = new();
    private readonly Mock<IDateTime> _dateTimeMock = new();
    private readonly ToolboxApp _app;

    public ToolboxTests()
    {
        _app = new ToolboxApp(
            _apiMock.Object,
            _fsMock.Object,
            _netMock.Object,
            _fixerMock.Object,
            _consoleMock.Object,
            _dateTimeMock.Object,
            "/test/script/dir"
        );
    }

    [Fact]
    public void ParseArguments_ShouldParseWeeklyShorts()
    {
        var args = new[] { "--weekly-shorts", "68" };
        var config = TrackmaniaCLI.ParseArguments(args);
        Assert.Equal("68", config.WeeklyShorts);
    }

    [Fact]
    public void ParseArguments_ShouldDefaultToLatest()
    {
        var args = new[] { "--weekly-shorts" };
        var config = TrackmaniaCLI.ParseArguments(args);
        Assert.Equal("latest", config.WeeklyShorts);
    }

    [Fact]
    public void ParseNumbers_ShouldParseSingleValue()
    {
        var result = _app.ParseNumbers("5");
        Assert.Equal(new List<int> { 5 }, result);
    }

    [Fact]
    public void ParseNumbers_ShouldParseCommaSeparated()
    {
        var result = _app.ParseNumbers("1,3,5");
        Assert.Equal(new List<int> { 1, 3, 5 }, result);
    }

    [Fact]
    public void ParseNumbers_ShouldParseRange()
    {
        var result = _app.ParseNumbers("1-3");
        Assert.Equal(new List<int> { 1, 2, 3 }, result);
    }

    [Fact]
    public void ParseNumbers_ShouldHandleMixed()
    {
        var result = _app.ParseNumbers("1, 5-7, 10");
        Assert.Equal(new List<int> { 1, 5, 6, 7, 10 }, result);
    }

    [Fact]
    public void FormatSeasonalFolderName_ShouldFormatCorrectly()
    {
        Assert.Equal("2024 - 1 - Winter", _app.FormatSeasonalFolderName("Winter 2024"));
        Assert.Equal("2024 - 2 - Spring", _app.FormatSeasonalFolderName("Spring 2024"));
        Assert.Equal("2024 - 3 - Summer", _app.FormatSeasonalFolderName("Summer 2024"));
        Assert.Equal("2024 - 4 - Fall", _app.FormatSeasonalFolderName("Fall 2024"));
    }

    [Fact]
    public async Task HandleWeeklyShorts_ShouldFilterByWeekNumberWithWordBoundary()
    {
        var campaign1 = new Mock<ICampaignItem>();
        campaign1.Setup(c => c.Id).Returns(6);
        campaign1.Setup(c => c.Name).Returns("Week 6");

        var campaign2 = new Mock<ICampaignItem>();
        campaign2.Setup(c => c.Id).Returns(60);
        campaign2.Setup(c => c.Name).Returns("Week 60");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaign1.Object, campaign2.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetWeeklyShortCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(Enumerable.Empty<IMap>());
        _apiMock.Setup(a => a.GetWeeklyShortCampaignAsync(6)).ReturnsAsync(campaignMock.Object);

        var config = TrackmaniaCLI.ParseArguments(Array.Empty<string>());

        await _app.HandleWeeklyShorts("6", config);

        _apiMock.Verify(a => a.GetWeeklyShortCampaignAsync(6), Times.Once);
        _apiMock.Verify(a => a.GetWeeklyShortCampaignAsync(60), Times.Never);
    }

    [Fact]
    public async Task HandleTrackOfTheDay_ShouldFallbackToYesterdayIfLatestMissingBefore17UTC()
    {
        var now = new DateTime(2024, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        _dateTimeMock.Setup(d => d.UtcNow).Returns(now);

        var yesterdayMap = new Mock<ITrackmaniaMap>();
        yesterdayMap.Setup(m => m.Name).Returns("Yesterday Map");
        yesterdayMap.Setup(m => m.FileUrl).Returns("http://example.com/map.gbx");

        var yesterdayDay = new Mock<ITrackOfTheDayDay>();
        yesterdayDay.Setup(d => d.MonthDay).Returns(14);
        yesterdayDay.Setup(d => d.Map).Returns(yesterdayMap.Object);

        var collectionMock = new Mock<ITrackOfTheDayCollection>();
        collectionMock.Setup(c => c.Days).Returns(new[] { yesterdayDay.Object }); // Only Day 14 exists
        collectionMock.Setup(c => c.Year).Returns(2024);
        collectionMock.Setup(c => c.Month).Returns(10);

        _apiMock.Setup(a => a.GetTrackOfTheDaysAsync(0)).ReturnsAsync(collectionMock.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--force" });
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[0]);

        await _app.HandleTrackOfTheDay("latest", config);

        _netMock.Verify(n => n.GetByteArrayAsync("http://example.com/map.gbx"), Times.Once);
    }

    [Fact]
    public async Task DownloadAndFixMaps_ShouldDeformatNamesAndApplyPrefixes()
    {
        var mapMock = new Mock<IMap>();
        mapMock.Setup(m => m.Name).Returns("$f00Formatted Name");
        mapMock.Setup(m => m.FileUrl).Returns("http://example.com/map.gbx");
        mapMock.Setup(m => m.FileName).Returns("OriginalFileName.Map.Gbx");

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(new[] { mapMock.Object });
        campaignMock.Setup(c => c.Name).Returns("Test Campaign");

        var campaignItemMock = new Mock<ICampaignItem>();
        campaignItemMock.Setup(c => c.Id).Returns(1);
        campaignItemMock.Setup(c => c.Name).Returns("Week 01");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaignItemMock.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetWeeklyShortCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);
        _apiMock.Setup(a => a.GetWeeklyShortCampaignAsync(1)).ReturnsAsync(campaignMock.Object);

        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[10]);
        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--weekly-shorts", "1" });
        await _app.RunAsync(config);

        // Expected filename: 01 - OriginalFileName.Map.Gbx
        string expectedPathEnding = "01 - OriginalFileName.Map.Gbx";

        _fsMock.Verify(f => f.WriteAllBytesAsync(It.Is<string>(s => s.EndsWith(expectedPathEnding)), It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public void RunBatchFixer_ShouldProcessExtraPaths()
    {
        var config = TrackmaniaCLI.ParseArguments(new[] { "--dry-run", "test-map.Map.Gbx" });

        _fsMock.Setup(f => f.FileExists("test-map.Map.Gbx")).Returns(true);
        _fsMock.Setup(f => f.DirectoryExists("test-map.Map.Gbx")).Returns(false);

        _app.RunBatchFixer(config, config.ExtraPaths);

        _fixerMock.Verify(f => f.ProcessFile("test-map.Map.Gbx", config), Times.Once);
    }

    [Fact]
    public async Task HandleTrackOfTheDay_ShouldFallbackToPreviousMonthIfLatestMissingOnFirstDay()
    {
        var now = new DateTime(2024, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        _dateTimeMock.Setup(d => d.UtcNow).Returns(now);

        var todayCollectionMock = new Mock<ITrackOfTheDayCollection>();
        todayCollectionMock.Setup(c => c.Days).Returns(Enumerable.Empty<ITrackOfTheDayDay>());
        todayCollectionMock.Setup(c => c.Year).Returns(2024);
        todayCollectionMock.Setup(c => c.Month).Returns(11);

        var yesterdayMap = new Mock<ITrackmaniaMap>();
        yesterdayMap.Setup(m => m.Name).Returns("Last Day Map");
        yesterdayMap.Setup(m => m.FileUrl).Returns("http://example.com/lastday.gbx");

        var yesterdayDay = new Mock<ITrackOfTheDayDay>();
        yesterdayDay.Setup(d => d.MonthDay).Returns(31);
        yesterdayDay.Setup(d => d.Map).Returns(yesterdayMap.Object);

        var yesterdayCollectionMock = new Mock<ITrackOfTheDayCollection>();
        yesterdayCollectionMock.Setup(c => c.Days).Returns(new[] { yesterdayDay.Object });
        yesterdayCollectionMock.Setup(c => c.Year).Returns(2024);
        yesterdayCollectionMock.Setup(c => c.Month).Returns(10);

        _apiMock.Setup(a => a.GetTrackOfTheDaysAsync(0)).ReturnsAsync(todayCollectionMock.Object);
        _apiMock.Setup(a => a.GetTrackOfTheDaysAsync(1)).ReturnsAsync(yesterdayCollectionMock.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--force" });
        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[0]);

        await _app.HandleTrackOfTheDay("latest", config);

        _netMock.Verify(n => n.GetByteArrayAsync("http://example.com/lastday.gbx"), Times.Once);
    }

    [Fact]
    public void ParseNumbers_ShouldHandleInvertedRange()
    {
        var result = _app.ParseNumbers("5-3");
        Assert.Equal(new List<int> { 3, 4, 5 }, result);
    }

    [Fact]
    public async Task DownloadAndFixMaps_ShouldSanitizeFilenames()
    {
        var mapMock = new Mock<IMap>();
        mapMock.Setup(m => m.Name).Returns("Invalid/Name*?");
        mapMock.Setup(m => m.FileUrl).Returns("http://example.com/map.gbx");

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(new[] { mapMock.Object });
        campaignMock.Setup(c => c.Name).Returns("Test Campaign");

        var campaignItemMock = new Mock<ICampaignItem>();
        campaignItemMock.Setup(c => c.Id).Returns(1);
        campaignItemMock.Setup(c => c.Name).Returns("Test Campaign");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaignItemMock.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);
        _apiMock.Setup(a => a.GetSeasonalCampaignAsync(1)).ReturnsAsync(campaignMock.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--force" });

        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[0]);
        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);

        await _app.HandleSeasonal("Test Campaign", config);

        // "/" and "*" and "?" should be replaced by "_"
        _fsMock.Verify(f => f.WriteAllBytesAsync(It.Is<string>(s => s.Contains("Invalid_Name__")), It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task HandleSeasonal_ShouldDeformatCampaignNameInOutput()
    {
        var campaignItemMock = new Mock<ICampaignItem>();
        campaignItemMock.Setup(c => c.Id).Returns(1);
        campaignItemMock.Setup(c => c.Name).Returns("$f00Formatted Campaign");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaignItemMock.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(Enumerable.Empty<IMap>());
        _apiMock.Setup(a => a.GetSeasonalCampaignAsync(1)).ReturnsAsync(campaignMock.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "latest" });
        await _app.HandleSeasonal("Formatted", config);

        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Found: Formatted Campaign"))), Times.Once);
    }

    [Fact]
    public async Task HandleExportCampaignMedals_ShouldCalculateMedalsCorrectly()
    {
        string playerId = Guid.NewGuid().ToString();

        var campaignItemMock = new Mock<ICampaignItem>();
        campaignItemMock.Setup(c => c.Id).Returns(1);
        campaignItemMock.Setup(c => c.Name).Returns("Seasonal Campaign");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaignItemMock.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);

        var mapMock = new Mock<IMap>();
        mapMock.Setup(m => m.Name).Returns("Map 1");
        mapMock.Setup(m => m.MapUid).Returns("uid1");
        mapMock.Setup(m => m.AuthorScore).Returns(TimeInt32.FromMilliseconds(10000));
        mapMock.Setup(m => m.GoldScore).Returns(TimeInt32.FromMilliseconds(20000));
        mapMock.Setup(m => m.SilverScore).Returns(TimeInt32.FromMilliseconds(30000));
        mapMock.Setup(m => m.BronzeScore).Returns(TimeInt32.FromMilliseconds(40000));

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(new[] { mapMock.Object });
        campaignMock.Setup(c => c.Name).Returns("Seasonal Campaign");

        _apiMock.Setup(a => a.GetSeasonalCampaignAsync(1)).ReturnsAsync(campaignMock.Object);

        var recordMock = new Mock<IRecord>();
        recordMock.Setup(r => r.Time).Returns(TimeInt32.FromMilliseconds(15000)); // Gold medal

        var leaderboardMock = new Mock<ILeaderboard>();
        leaderboardMock.Setup(l => l.Tops).Returns(new[] { recordMock.Object });

        _apiMock.Setup(a => a.GetLeaderboardAsync("uid1", playerId)).ReturnsAsync(leaderboardMock.Object);

        await _app.HandleExportCampaignMedals(playerId, "Seasonal");

        _fsMock.Verify(f => f.WriteAllLines("medals.csv", It.Is<IEnumerable<string>>(lines =>
            lines.Any(l => l.Contains("Medal: 3") || l.Contains(", 3,")) // Medal 3 is Gold
        )), Times.Once);
    }

    [Fact]
    public async Task DownloadAndFixMaps_ShouldSkipIfFileExists()
    {
        var mapMock = new Mock<IMap>();
        mapMock.Setup(m => m.Name).Returns("Existing Map");
        mapMock.Setup(m => m.FileUrl).Returns("http://example.com/map.gbx");

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(new[] { mapMock.Object });
        campaignMock.Setup(c => c.Name).Returns("Test Campaign");

        var campaignItemMock = new Mock<ICampaignItem>();
        campaignItemMock.Setup(c => c.Id).Returns(1);
        campaignItemMock.Setup(c => c.Name).Returns("Test Campaign");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaignItemMock.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);
        _apiMock.Setup(a => a.GetSeasonalCampaignAsync(1)).ReturnsAsync(campaignMock.Object);

        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true); // File already exists

        var config = TrackmaniaCLI.ParseArguments(Array.Empty<string>()); // No --force

        await _app.HandleSeasonal("Test Campaign", config);

        _netMock.Verify(n => n.GetByteArrayAsync(It.IsAny<string>()), Times.Never);
        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Skipped (already exists)"))), Times.Once);
    }

    [Fact]
    public async Task HandleClubCampaign_ShouldHandleMultipleMatchesNonInteractively()
    {
        var campaign1 = new Mock<ICampaignItem>();
        campaign1.Setup(c => c.Id).Returns(1);
        campaign1.Setup(c => c.Name).Returns("Campaign A");
        campaign1.Setup(c => c.ClubId).Returns(100);

        var campaign2 = new Mock<ICampaignItem>();
        campaign2.Setup(c => c.Id).Returns(2);
        campaign2.Setup(c => c.Name).Returns("Campaign B");
        campaign2.Setup(c => c.ClubId).Returns(200);

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaign1.Object, campaign2.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetClubCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(Enumerable.Empty<IMap>());
        _apiMock.Setup(a => a.GetClubCampaignAsync(100, 1)).ReturnsAsync(campaignMock.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--non-interactive" });

        await _app.HandleClubCampaign("Campaign", config);

        // Should have picked the first match
        _apiMock.Verify(a => a.GetClubCampaignAsync(100, 1), Times.Once);
        _apiMock.Verify(a => a.GetClubCampaignAsync(200, 2), Times.Never);
    }

    [Fact]
    public async Task HandleExportCampaignMedals_ShouldCatchApiExceptions()
    {
        string playerId = Guid.NewGuid().ToString();

        var campaignItemMock = new Mock<ICampaignItem>();
        campaignItemMock.Setup(c => c.Id).Returns(1);
        campaignItemMock.Setup(c => c.Name).Returns("Seasonal Campaign");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaignItemMock.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);

        var mapMock = new Mock<IMap>();
        mapMock.Setup(m => m.Name).Returns("Map 1");
        mapMock.Setup(m => m.MapUid).Returns("uid1");

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(new[] { mapMock.Object });
        campaignMock.Setup(c => c.Name).Returns("Seasonal Campaign");

        _apiMock.Setup(a => a.GetSeasonalCampaignAsync(1)).ReturnsAsync(campaignMock.Object);

        // API throws exception (e.g. 500)
        _apiMock.Setup(a => a.GetLeaderboardAsync("uid1", playerId)).ThrowsAsync(new HttpRequestException("API Error"));

        await _app.HandleExportCampaignMedals(playerId, "Seasonal");

        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Error or no record"))), Times.Once);
        _fsMock.Verify(f => f.WriteAllLines("medals.csv", It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public async Task HandleTrackOfTheDay_ShouldHandleRangeAndMonthFormats()
    {
        _dateTimeMock.Setup(d => d.UtcNow).Returns(new DateTime(2024, 10, 15));

        var collectionMock = new Mock<ITrackOfTheDayCollection>();
        collectionMock.Setup(c => c.Year).Returns(2024);
        collectionMock.Setup(c => c.Month).Returns(10);

        var day1 = new Mock<ITrackOfTheDayDay>();
        day1.Setup(d => d.MonthDay).Returns(1);
        day1.Setup(d => d.Map).Returns(new Mock<ITrackmaniaMap>().Object);

        var day2 = new Mock<ITrackOfTheDayDay>();
        day2.Setup(d => d.MonthDay).Returns(2);
        day2.Setup(d => d.Map).Returns(new Mock<ITrackmaniaMap>().Object);

        collectionMock.Setup(c => c.Days).Returns(new[] { day1.Object, day2.Object });

        _apiMock.Setup(a => a.GetTrackOfTheDaysAsync(0)).ReturnsAsync(collectionMock.Object);

        var config = TrackmaniaCLI.ParseArguments(Array.Empty<string>());

        // Range: 2024-10 1-2
        await _app.HandleTrackOfTheDay("2024-10-1-2", config);

        _consoleMock.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Processing 2 maps"))), Times.Once);
    }

    [Fact]
    public async Task HandleTmxMaps_ShouldUseCorrectDownloadUrl()
    {
        var mapMock = new Mock<ITmxMap>();
        mapMock.Setup(m => m.Id).Returns(18101);
        mapMock.Setup(m => m.Name).Returns("Contact");

        _apiMock.Setup(a => a.GetTmxMapAsync(18101)).ReturnsAsync(mapMock.Object);
        _apiMock.Setup(a => a.GetTmxMapUrl(18101)).Returns("https://trackmania.exchange/mapgbx/18101");

        _netMock.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync(new byte[0]);
        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--tmx", "18101" });
        await _app.RunAsync(config);

        _netMock.Verify(n => n.GetByteArrayAsync("https://trackmania.exchange/mapgbx/18101"), Times.Once);
    }

    [Fact]
    public async Task SearchTmxMaps_ShouldMapSortStringsCorrectly()
    {
        _apiMock.Setup(a => a.SearchTmxMapsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(Enumerable.Empty<ITmxMap>());

        var config = TrackmaniaCLI.ParseArguments(new[] { "--tmx-search", "test", "--tmx-sort", "awards", "--tmx-desc" });
        await _app.RunAsync(config);

        _apiMock.Verify(a => a.SearchTmxMapsAsync("test", null, "awards", true), Times.Once);
    }

    [Fact]
    public async Task SelectItemAsync_ShouldBeCalledWhenMultipleCampaignsFound()
    {
        var campaign1 = new Mock<ICampaignItem>();
        campaign1.Setup(c => c.Id).Returns(1);
        campaign1.Setup(c => c.Name).Returns("Campaign 1");

        var campaign2 = new Mock<ICampaignItem>();
        campaign2.Setup(c => c.Id).Returns(2);
        campaign2.Setup(c => c.Name).Returns("Campaign 2");

        var collectionMock = new Mock<ICampaignCollection>();
        collectionMock.Setup(c => c.Campaigns).Returns(new[] { campaign1.Object, campaign2.Object });
        collectionMock.Setup(c => c.PageCount).Returns(1);

        _apiMock.Setup(a => a.GetSeasonalCampaignsAsync(0)).ReturnsAsync(collectionMock.Object);
        _consoleMock.Setup(c => c.SelectItemAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(1);

        var campaignMock = new Mock<ICampaign>();
        campaignMock.Setup(c => c.Playlist).Returns(Enumerable.Empty<IMap>());
        _apiMock.Setup(a => a.GetSeasonalCampaignAsync(1)).ReturnsAsync(campaignMock.Object);

        var config = TrackmaniaCLI.ParseArguments(new[] { "--seasonal", "Campaign" });
        await _app.RunAsync(config);

        _consoleMock.Verify(c => c.SelectItemAsync(It.Is<string>(s => s.Contains("Multiple")), It.IsAny<IEnumerable<string>>()), Times.Once);
    }
}

public class RuntimeTests
{
    [Fact]
    public void GetScriptDirectory_ShouldReturnCurrentBaseDirectory()
    {
        var result = TrackmaniaCLI.GetScriptDirectory();

        Assert.NotNull(result);
        Assert.True(Directory.Exists(result), $"Directory should exist: {result}");

        // Ensure it's not a source path (baking check)
        Assert.DoesNotContain("Trackmania2020Toolbox.Core", result);

        // It should match the actual app domain base directory
        Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, result);
    }
}
