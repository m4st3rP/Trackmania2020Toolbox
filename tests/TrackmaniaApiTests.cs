using Xunit;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;

namespace Trackmania2020Toolbox.Tests;

public class TrackmaniaApiTests
{
    [Fact]
    public async Task Dispose_ShouldNotDisposeSharedHttpClient()
    {
        // Arrange
        var httpClient = new HttpClient();
        var userAgent = "TestUserAgent";
        var consoleMock = new Mock<IConsole>();
        var wrapper = new TrackmaniaApiWrapper(httpClient, userAgent, consoleMock.Object);

        // Act
        wrapper.Dispose();

        // Assert
        // If the HttpClient was disposed, accessing a property like BaseAddress
        // (even if null) or sending a request would throw ObjectDisposedException.
        // We'll check if we can still use it.
        var exception = await Record.ExceptionAsync(() => httpClient.GetAsync("http://localhost:12345"));

        // We expect a HttpRequestException or similar because the port is likely closed,
        // but NOT an ObjectDisposedException.
        Assert.IsNotType<System.ObjectDisposedException>(exception);

        httpClient.Dispose(); // Clean up for the test
    }
}
