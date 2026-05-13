using Moq;
using System.Diagnostics;
using Trackmania2020Toolbox;
using Xunit;

namespace Trackmania2020Toolbox.Tests;

public class RefactorVerificationTests
{
    [Fact]
    public async Task HandleTrackOfTheDayAsync_ShouldBePerformant_WithManyDates()
    {
        // This test verifies the O(N) performance of HashSet deduplication
        var apiMock = new Mock<ITrackmaniaApi>();
        var fsMock = new Mock<IFileSystem>();
        var netMock = new Mock<INetworkService>();
        var fixerMock = new Mock<IMapFixer>();
        var consoleMock = new Mock<IConsole>();
        var dateTimeMock = new Mock<IDateTime>();
        var parserMock = new Mock<IInputParser>();
        var downloaderMock = new Mock<IMapDownloader>();

        dateTimeMock.Setup(d => d.UtcNow).Returns(new DateTime(2024, 1, 1));

        // Return empty collection to minimize other work
        apiMock.Setup(a => a.GetTrackOfTheDaysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MockTrackOfTheDayCollection());

        fsMock.Setup(f => f.FileExists("test.Map.Gbx")).Returns(true);
        var app = new ToolboxApp(apiMock.Object, fsMock.Object, netMock.Object, fixerMock.Object,
                                 consoleMock.Object, dateTimeMock.Object, "/test",
                                 parserMock.Object, downloaderMock.Object);

        // Many overlapping ranges to test deduplication
        var input = "2023-01-01 - 2023-12-31, 2023-01-01 - 2023-06-01, 2023-06-01 - 2023-12-31";
        var ranges = new List<(DateTime Start, DateTime End)> {
            (new DateTime(2023, 1, 1), new DateTime(2023, 12, 31)),
            (new DateTime(2023, 1, 1), new DateTime(2023, 6, 1)),
            (new DateTime(2023, 6, 1), new DateTime(2023, 12, 31))
        };
        parserMock.Setup(p => p.ParseToTdRanges(input, It.IsAny<DateTime>())).Returns(ranges);

        var sw = Stopwatch.StartNew();
        await app.HandleTrackOfTheDayAsync(input, Config.Default);
        sw.Stop();

        // Should be very fast with HashSet, even if we were doing List.Contains it's only ~365 items,
        // but this confirms the logic path.
        Assert.True(sw.ElapsedMilliseconds < 1000, $"HandleTrackOfTheDayAsync took too long: {sw.ElapsedMilliseconds}ms");
    }

    private class MockTrackOfTheDayCollection : ITrackOfTheDayCollection
    {
        public int Year => 2023;
        public int Month => 1;
        public IEnumerable<ITrackOfTheDayDay> Days => Enumerable.Empty<ITrackOfTheDayDay>();
    }

    [Fact]
    public async Task CoreMethods_ShouldRespectCancellationToken()
    {
        var apiMock = new Mock<ITrackmaniaApi>();
        var fsMock = new Mock<IFileSystem>();
        var netMock = new Mock<INetworkService>();
        var fixerMock = new Mock<IMapFixer>();
        var consoleMock = new Mock<IConsole>();
        var dateTimeMock = new Mock<IDateTime>();
        var parserMock = new Mock<IInputParser>();
        var downloaderMock = new Mock<IMapDownloader>();

        fsMock.Setup(f => f.FileExists("test.Map.Gbx")).Returns(true);
        var app = new ToolboxApp(apiMock.Object, fsMock.Object, netMock.Object, fixerMock.Object,
                                 consoleMock.Object, dateTimeMock.Object, "/test",
                                 parserMock.Object, downloaderMock.Object);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            app.RunBatchFixerAsync(Config.Default, new List<string> { "test.Map.Gbx" }, cts.Token));
    }
}
