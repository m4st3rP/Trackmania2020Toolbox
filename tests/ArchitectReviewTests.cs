using Moq;
using Xunit;
using Trackmania2020Toolbox;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Trackmania2020Toolbox.Tests;

public class ArchitectReviewTests
{
    [Fact]
    public void RealBrowserService_GetBrowserItems_FiltersAndSortsCorrectly()
    {
        // Arrange
        var mockFs = new Mock<IFileSystem>();
        var directory = "Maps";
        var filter = "test";

        mockFs.Setup(f => f.DirectoryExists(directory)).Returns(true);
        mockFs.Setup(f => f.GetDirectories(directory)).Returns(["Maps/TestDir", "Maps/OtherDir"]);
        mockFs.Setup(f => f.GetFiles(directory, "*.Map.Gbx", SearchOption.TopDirectoryOnly))
              .Returns(["Maps/test_map.Map.Gbx", "Maps/other_map.Map.Gbx"]);

        var service = new RealBrowserService(mockFs.Object);

        // Act
        var items = service.GetBrowserItems(directory, filter, descending: false).ToList();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.DisplayName == "TestDir" && i.IsDirectory);
        Assert.Contains(items, i => i.DisplayName == "test_map.Map.Gbx" && !i.IsDirectory);
        Assert.DoesNotContain(items, i => i.DisplayName == "OtherDir");
        Assert.DoesNotContain(items, i => i.DisplayName == "other_map.Map.Gbx");
    }

    [Fact]
    public void InputParser_ParseNumbers_LogsErrorOnInvalidInput()
    {
        // Arrange
        var mockConsole = new Mock<IConsole>();
        var parser = new InputParser(mockConsole.Object);
        var input = "1, abc, 3-5, 10-xyz";

        // Act
        var result = parser.ParseNumbers(input);

        // Assert
        Assert.Equal([1, 3, 4, 5], result);
        mockConsole.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Could not parse number 'abc'"))), Times.Once);
        mockConsole.Verify(c => c.WriteLine(It.Is<string>(s => s.Contains("Could not parse number range '10-xyz'"))), Times.Once);
    }

    [Fact]
    public void InputParser_ParseSeasonalRef_HandlesNullAndEmpty()
    {
        // Arrange
        var mockConsole = new Mock<IConsole>();
        var parser = new InputParser(mockConsole.Object);

        // Act & Assert
        Assert.Null(parser.ParseSeasonalRef(""));
        Assert.Null(parser.ParseSeasonalRef("   "));

        var valid = parser.ParseSeasonalRef("Winter 2024");
        Assert.NotNull(valid);
        Assert.Equal(2024, valid.Year);
        Assert.Equal(1, valid.SeasonOrder);
    }

    [Fact]
    public async Task MapDownloader_SanitizesFilenamesCorrectly()
    {
        // Arrange
        var mockFs = new Mock<IFileSystem>();
        var mockNet = new Mock<INetworkService>();
        var mockFixer = new Mock<IMapFixer>();
        var mockConsole = new Mock<IConsole>();

        var downloader = new MapDownloader(mockFs.Object, mockNet.Object, mockFixer.Object, mockConsole.Object);
        var maps = new List<MapDownloadRecord>
        {
            new("Map: With/Invalid*Chars", null, "http://url", "Prefix_")
        };
        var config = Config.Default;
        var dir = "Downloads";

        mockFs.Setup(f => f.DirectoryExists(dir)).Returns(true);
        mockNet.Setup(n => n.GetByteArrayAsync(It.IsAny<string>())).ReturnsAsync([]);
        mockFs.Setup(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

        // Act
        await downloader.DownloadAndFixMapsAsync(maps, dir, config);

        // Assert
        string expectedFileName = "Prefix_Map_ With_Invalid_Chars.Map.Gbx";
        string expectedPath = Path.Combine(dir, expectedFileName);
        mockFs.Verify(f => f.WriteAllBytesAsync(expectedPath, It.IsAny<byte[]>()), Times.Once);
    }
}
