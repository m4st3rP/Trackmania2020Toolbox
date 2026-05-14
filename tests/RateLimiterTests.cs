using System.Diagnostics;
using System.Net.Http;
using Moq;
using Trackmania2020Toolbox;
using Xunit;

namespace Trackmania2020Toolbox.Tests;

public class RateLimiterTests
{
    [Fact]
    public async Task RateLimiter_ShouldRespectDelayBetweenCalls()
    {
        using var httpClient = new HttpClient();
        var consoleMock = new Mock<IConsole>();
        var limiter = new TrackmaniaApiWrapper(httpClient, "Test", consoleMock.Object) { DelayMs = 100 };
        var sw = Stopwatch.StartNew();

        await limiter.ApplyDelayAsync(); // First call, no delay
        await limiter.ApplyDelayAsync(); // Second call, should delay ~100ms

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds >= 90, $"Elapsed: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task RateLimiter_ShouldSerializeConcurrentCalls()
    {
        using var httpClient = new HttpClient();
        var consoleMock = new Mock<IConsole>();
        var limiter = new TrackmaniaApiWrapper(httpClient, "Test", consoleMock.Object) { DelayMs = 100 };
        var sw = Stopwatch.StartNew();

        var task1 = limiter.ApplyDelayAsync();
        var task2 = limiter.ApplyDelayAsync();
        var task3 = limiter.ApplyDelayAsync();

        await Task.WhenAll(task1, task2, task3);

        sw.Stop();
        // 3 calls with 100ms delay between them:
        // Call 1: 0ms
        // Call 2: 100ms
        // Call 3: 200ms
        // Total elapsed should be at least ~200ms
        Assert.True(sw.ElapsedMilliseconds >= 190, $"Elapsed: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task RateLimiter_ShouldLogWhenDelayExceeds500ms()
    {
        using var httpClient = new HttpClient();
        var consoleMock = new Mock<IConsole>();
        var limiter = new TrackmaniaApiWrapper(httpClient, "Test", consoleMock.Object) { DelayMs = 600 };

        await limiter.ApplyDelayAsync(); // First call
        await limiter.ApplyDelayAsync(); // Second call, should log

        consoleMock.Verify(c => c.WriteLine("Waiting for rate limit..."), Times.Once);
    }
}
