using Xunit;
using Trackmania2020Toolbox;
using Moq;
using System.Collections.Generic;

namespace Trackmania2020Toolbox.Tests;

public class RefactorVerificationTests
{
    [Fact]
    public void ParseTmxIds_ShouldHandleUrlsAndIds()
    {
        var console = new Mock<IConsole>();
        var parser = new InputParser(console.Object);
        var input = "123, https://trackmania.exchange/maps/456, https://trackmania.exchange/mappack/789 1011";

        var result = parser.ParseTmxIds(input);

        Assert.Equal([123, 456, 789, 1011], result);
    }

    [Fact]
    public void ParseTmxIds_ShouldHandleInvalidInputGracefully()
    {
        var console = new Mock<IConsole>();
        var parser = new InputParser(console.Object);
        var input = "abc, 123, https://invalid.url/123";

        var result = parser.ParseTmxIds(input);

        Assert.Equal([123], result);
        console.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Could not parse TMX ID"))), Times.Exactly(2));
    }
}
