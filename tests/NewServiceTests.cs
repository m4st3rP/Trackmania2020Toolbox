using Moq;
using Xunit;

namespace Trackmania2020Toolbox.Tests;

public class InputParserTests
{
    private readonly Mock<IConsole> _consoleMock = new();
    private readonly InputParser _parser;

    public InputParserTests()
    {
        _parser = new InputParser(_consoleMock.Object);
    }

    [Fact]
    public void ParseWeeklyShortsNum_ShouldExtractCorrectNumber()
    {
        Assert.Equal(68, _parser.ParseWeeklyShortsNum("Week 68"));
        Assert.Equal(5, _parser.ParseWeeklyShortsNum("Week 05"));
        Assert.Equal(-1, _parser.ParseWeeklyShortsNum("Weekly Grand 65"));
    }

    [Fact]
    public void ParseWeeklyGrandsNum_ShouldExtractCorrectNumber()
    {
        Assert.Equal(65, _parser.ParseWeeklyGrandsNum("Weekly Grand 65"));
        Assert.Equal(65, _parser.ParseWeeklyGrandsNum("Week Grand 65"));
        Assert.Equal(-1, _parser.ParseWeeklyGrandsNum("Week 68"));
    }

    [Theory]
    [InlineData("2024.10.15", 2024, 10, 15)]
    [InlineData("2024/10/15", 2024, 10, 15)]
    public void ParseToTdRanges_ShouldHandleDifferentSeparators(string input, int y, int m, int d)
    {
        var now = new DateTime(2025, 1, 1);
        var result = _parser.ParseToTdRanges(input, now);
        Assert.Single(result);
        Assert.Equal(new DateTime(y, m, d), result[0].Start);
        Assert.Equal(new DateTime(y, m, d), result[0].End);
    }

    [Fact]
    public void ParseSeasonalRef_ShouldHandleYearOnly()
    {
        var result = _parser.ParseSeasonalRef("2024");
        Assert.NotNull(result);
        Assert.Equal(2024, result.Year);
        Assert.Equal(1, result.SeasonOrder);
    }

    [Fact]
    public void ParseSeasonalRefFromCampaignName_ShouldHandleOfficialFormat()
    {
        var result = _parser.ParseSeasonalRefFromCampaignName("Summer 2022");
        Assert.NotNull(result);
        Assert.Equal(2022, result.Year);
        Assert.Equal(3, result.SeasonOrder);
    }

    [Fact]
    public void ParseToTdRanges_ShouldHandleCrossYearRollover()
    {
        var now = new DateTime(2024, 12, 31);
        var result = _parser.ParseToTdRanges("12.30-01.05", now);

        Assert.Single(result);
        Assert.Equal(new DateTime(2024, 12, 30), result[0].Start);
        Assert.Equal(new DateTime(2025, 1, 5), result[0].End);
    }

    [Fact]
    public void ParseMapRanges_ShouldHandleMapPrecision()
    {
        var result = _parser.ParseMapRanges("21.3-23.1");
        Assert.Single(result);
        Assert.Equal(21, result[0].Start.Campaign);
        Assert.Equal(3, result[0].Start.Map);
        Assert.Equal(23, result[0].End.Campaign);
        Assert.Equal(1, result[0].End.Map);
    }
}

public class MapDownloaderTests
{
    private readonly Mock<IFileSystem> _fsMock = new();
    private readonly Mock<INetworkService> _netMock = new();
    private readonly Mock<IMapFixer> _fixerMock = new();
    private readonly Mock<IConsole> _consoleMock = new();
    private readonly MapDownloader _downloader;

    public MapDownloaderTests()
    {
        _downloader = new MapDownloader(_fsMock.Object, _netMock.Object, _fixerMock.Object, _consoleMock.Object);
    }

    [Fact]
    public async Task DownloadAndFixMapsAsync_ShouldCreateDirectoryIfNotExists()
    {
        _fsMock.Setup(f => f.DirectoryExists("test-dir")).Returns(false);
        var maps = new[] { new MapDownloadRecord("Map", null, "http://url", null) };
        var config = Config.Default;

        await _downloader.DownloadAndFixMapsAsync(maps, "test-dir", config);

        _fsMock.Verify(f => f.CreateDirectory("test-dir"), Times.Once);
    }

    [Fact]
    public async Task DownloadAndFixMapsAsync_ShouldSkipExistingFilesWithoutForce()
    {
        _fsMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fsMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        var maps = new[] { new MapDownloadRecord("Map", null, "http://url", null) };
        var config = Config.Default;
        config.App.ForceOverwrite = false;

        await _downloader.DownloadAndFixMapsAsync(maps, "dir", config);

        _netMock.Verify(n => n.GetByteArrayAsync(It.IsAny<string>()), Times.Never);
    }
}
