using System.Diagnostics;
using System.Net.Http;
using Trackmania2020Toolbox;
using Xunit;

namespace Trackmania2020Toolbox.Tests;

public class RateLimiterTests
{
    [Fact]
    public async Task RateLimiter_ShouldRespectDelayBetweenCalls()
    {
        using var httpClient = new HttpClient();
        var limiter = new TrackmaniaApiWrapper(httpClient, "Test") { DelayMs = 100 };
        var sw = Stopwatch.StartNew();

        await limiter.ApplyDelayAsync(CancellationToken.None); // First call, no delay
        await limiter.ApplyDelayAsync(CancellationToken.None); // Second call, should delay ~100ms

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds >= 90, $"Elapsed: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task RateLimiter_ShouldSerializeConcurrentCalls()
    {
        using var httpClient = new HttpClient();
        var limiter = new TrackmaniaApiWrapper(httpClient, "Test") { DelayMs = 100 };
        var sw = Stopwatch.StartNew();

        var task1 = limiter.ApplyDelayAsync(CancellationToken.None);
        var task2 = limiter.ApplyDelayAsync(CancellationToken.None);
        var task3 = limiter.ApplyDelayAsync(CancellationToken.None);

        await Task.WhenAll(task1, task2, task3);

        sw.Stop();
        // 3 calls with 100ms delay between them:
        // Call 1: 0ms
        // Call 2: 100ms
        // Call 3: 200ms
        // Total elapsed should be at least ~200ms
        Assert.True(sw.ElapsedMilliseconds >= 190, $"Elapsed: {sw.ElapsedMilliseconds}ms");
    }
}
