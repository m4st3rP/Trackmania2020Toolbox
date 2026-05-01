using Xunit;
using Trackmania2020Toolbox;

namespace Trackmania2020Toolbox.Tests;

public class UtilityTests
{
    [Theory]
    [InlineData("simple.gbx", "simple.gbx")]
    [InlineData("name/with/slashes", "name_with_slashes")]
    [InlineData("name:with:colons", "name_with_colons")]
    [InlineData("name*with*stars", "name_with_stars")]
    [InlineData("name?with?question", "name_with_question")]
    [InlineData("name\"with\"quotes", "name_with_quotes")]
    [InlineData("name<with>brackets", "name_with_brackets")]
    [InlineData("name|with|pipe", "name_with_pipe")]
    public void SanitizeString_ShouldReplaceInvalidChars(string input, string expected)
    {
        var result = PathUtilities.SanitizeString(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  folder name  ", "folder name")]
    [InlineData("folder/name ", "folder_name")]
    public void SanitizeFolderName_ShouldSanitizeAndTrim(string input, string expected)
    {
        var result = PathUtilities.SanitizeFolderName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("comma,separated", "\"comma,separated\"")]
    [InlineData("quote\"inside", "\"quote\"\"inside\"")]
    [InlineData("newline\ninside", "\"newline\ninside\"")]
    [InlineData("return\rinside", "\"return\rinside\"")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void EscapeCsv_ShouldHandleSpecialCharacters(string? input, string? expected)
    {
        var result = CsvUtilities.EscapeCsv(input!);
        Assert.Equal(expected, result);
    }
}
