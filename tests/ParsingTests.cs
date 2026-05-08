using Xunit;
using Trackmania2020Toolbox;
using Moq;

namespace Trackmania2020Toolbox.Tests;

public class ParsingTests
{
    private readonly Mock<IConsole> _consoleMock = new();

    [Theory]
    [InlineData("1", "1", null)]
    [InlineData("1-5", "1", "5")]
    [InlineData("1 - 5", "1", "5")]
    [InlineData("1, 5", "1", null, "5", null)]
    [InlineData("2024-01-01", "2024-01-01", null)]
    [InlineData("2024-01-01 - 2024-01-10", "2024-01-01", "2024-01-10")]
    [InlineData("2024-01-01, 2024-02-01", "2024-01-01", null, "2024-02-01", null)]
    [InlineData("2024-01", "2024-01", null)]
    [InlineData("1.1-1.5", "1.1", "1.5")]
    [InlineData("1.1 - 2.3", "1.1", "2.3")]
    public void ForEachRange_ShouldParseCorrectly(string input, params string?[] expected)
    {
        var results = new List<(string, string?)>();

        InputParser.ForEachRange(input, new char[] { ',', ' ' }, (s, e) => results.Add((s, e)));

        Assert.Equal(expected.Length / 2, results.Count);
        for (int i = 0; i < results.Count; i++)
        {
            Assert.Equal(expected[i * 2], results[i].Item1);
            Assert.Equal(expected[i * 2 + 1], results[i].Item2);
        }
    }

    [Fact]
    public void ForEachRange_ShouldIgnoreIsoDatesAsRanges()
    {
        var results = new List<(string, string?)>();

        InputParser.ForEachRange("2024-01-01", new char[] { ',', ' ' }, (s, e) => results.Add((s, e)));

        Assert.Single(results);
        Assert.Equal("2024-01-01", results[0].Item1);
        Assert.Null(results[0].Item2);
    }

    [Fact]
    public void ForEachRange_ShouldHandleMonthFormat()
    {
        var results = new List<(string, string?)>();

        InputParser.ForEachRange("2024-01", new char[] { ',', ' ' }, (s, e) => results.Add((s, e)));

        Assert.Single(results);
        Assert.Equal("2024-01", results[0].Item1);
        Assert.Null(results[0].Item2);
    }

    [Fact]
    public void ForEachRange_ShouldHandleSpaceSeparatedRanges()
    {
        var results = new List<(string, string?)>();

        InputParser.ForEachRange("2024-01-01 - 2024-01-05", new char[] { ',', ' ' }, (s, e) => results.Add((s, e)));

        Assert.Single(results);
        Assert.Equal("2024-01-01", results[0].Item1);
        Assert.Equal("2024-01-05", results[0].Item2);
    }
}
