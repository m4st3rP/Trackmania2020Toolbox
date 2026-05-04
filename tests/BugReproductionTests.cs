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
}
