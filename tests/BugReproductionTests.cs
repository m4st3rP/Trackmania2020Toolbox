using Xunit;
using Trackmania2020Toolbox;
using Moq;
using System;

namespace Trackmania2020Toolbox.Tests;

public class BugReproductionTests
{
    private readonly Mock<IConsole> _consoleMock = new();
    private readonly InputParser _parser;

    public BugReproductionTests()
    {
        _parser = new InputParser(_consoleMock.Object);
    }

    [Theory]
    [InlineData("2024.01.00")]
    [InlineData("2024.00.01")]
    [InlineData("2024.13.01")]
    [InlineData("00.01")]
    [InlineData("01.00")]
    [InlineData("13.01")]
    public void ParseToTdRanges_ShouldNotThrow_WhenInputIsInvalid(string input)
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges(input, now);
        Assert.Empty(ranges);
    }

    [Theory]
    [InlineData("2024.01.01-00")]
    [InlineData("2024.01.01-01.00")]
    [InlineData("2024.01.01-00.01")]
    public void ParseToTdRanges_ShouldNotThrow_WhenRangeEndIsInvalid(string input)
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges(input, now);
        // It might be empty or just contain the valid start, but shouldn't throw.
        // If the start is valid but end is not, currently it logs an error and skips the range.
    }

    [Fact]
    public void ParseToTdRanges_ShouldHandleValidInput()
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges("2024.01.15", now);
        Assert.Single(ranges);
        Assert.Equal(new DateTime(2024, 1, 15), ranges[0].Start);
    }

    [Fact]
    public void ParseToTdRanges_ShouldHandleFullDateRange()
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges("2024.01.01-2024.01.15", now);
        Assert.Single(ranges);
        Assert.Equal(new DateTime(2024, 1, 1), ranges[0].Start);
        Assert.Equal(new DateTime(2024, 1, 15), ranges[0].End);
    }

    [Fact]
    public void ParseToTdRanges_ShouldHandleSpacesInRange()
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges("2024.01.01 - 2024.01.15", now);
        Assert.Single(ranges);
        Assert.Equal(new DateTime(2024, 1, 1), ranges[0].Start);
        Assert.Equal(new DateTime(2024, 1, 15), ranges[0].End);
    }

    [Fact]
    public void ParseToTdRanges_ShouldNormalizeRanges()
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges("2024.01.15-2024.01.01", now);
        Assert.Single(ranges);
        Assert.Equal(new DateTime(2024, 1, 1), ranges[0].Start);
        Assert.Equal(new DateTime(2024, 1, 15), ranges[0].End);
    }

    [Fact]
    public void ParseToTdRanges_ShouldNormalizeShortHandRanges()
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges("2024.01.15-01", now);
        Assert.Single(ranges);
        Assert.Equal(new DateTime(2024, 1, 1), ranges[0].Start);
        Assert.Equal(new DateTime(2024, 1, 15), ranges[0].End);
    }

    [Fact]
    public void ParseToTdRanges_ShouldHandleDatesWithDashes()
    {
        var now = new DateTime(2024, 1, 1);
        var ranges = _parser.ParseToTdRanges("2024-01-01 - 2024-01-15", now);
        Assert.Single(ranges);
        Assert.Equal(new DateTime(2024, 1, 1), ranges[0].Start);
        Assert.Equal(new DateTime(2024, 1, 15), ranges[0].End);
    }

    [Fact]
    public void ParseNumbers_ShouldHandleSpacesInRange()
    {
        var nums = _parser.ParseNumbers("1 , 3-5 , 10 - 12");
        Assert.Equal([1, 3, 4, 5, 10, 11, 12], nums);
    }

    [Fact]
    public async Task GetTrackOfTheDaysAsync_ShouldHaveCacheCollision_WhenMonthChanges()
    {
        // Arrange
        var mockInnerApi = new Mock<ITrackmaniaApi>();
        var mockFs = new Mock<IFileSystem>();
        var mockDateTime = new Mock<IDateTime>();
        var config = new CacheConfig { Enabled = true, CacheDirectory = ".cache" };
        var scriptDir = "/test";

        var janData = new TrackOfTheDayCollectionDto { Year = 2024, Month = 1 };
        var febData = new TrackOfTheDayCollectionDto { Year = 2024, Month = 2 };

        // 1. Initial request in January, just before rollover
        mockDateTime.Setup(d => d.UtcNow).Returns(new DateTime(2024, 1, 31, 23, 59, 0));
        mockInnerApi.Setup(a => a.GetTrackOfTheDaysAsync(0)).ReturnsAsync(janData);

        // Simulating file system for cache
        var files = new Dictionary<string, string>();
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFs.Setup(f => f.FileExists(It.IsAny<string>())).Returns<string>(path => files.ContainsKey(path));
        mockFs.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).Returns<string>(path => Task.FromResult(files[path]));
        mockFs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, content) => files[path] = content)
            .Returns(Task.CompletedTask);

        var cachedApi = new CachedTrackmaniaApi(mockInnerApi.Object, mockFs.Object, mockDateTime.Object, scriptDir, config);

        // Act
        var result1 = await cachedApi.GetTrackOfTheDaysAsync(0);

        // 2. Move to February, just after rollover
        // Only 2 minutes have passed, well within the 60-minute DynamicExpirationMinutes
        mockDateTime.Setup(d => d.UtcNow).Returns(new DateTime(2024, 2, 1, 0, 1, 0));
        mockInnerApi.Setup(a => a.GetTrackOfTheDaysAsync(0)).ReturnsAsync(febData);

        var result2 = await cachedApi.GetTrackOfTheDaysAsync(0);

        // Assert
        Assert.Equal(1, result1.Month);

        // After fix: result2.Month should be 2 because the cache key includes the absolute year/month
        Assert.Equal(2, result2.Month);
    }
}
