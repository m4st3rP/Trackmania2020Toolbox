using System.Buffers;

namespace Trackmania2020Toolbox;

public static class PathUtilities
{
    private static readonly SearchValues<char> InvalidFileNameChars = SearchValues.Create(
        Path.GetInvalidFileNameChars()
        .Union(['/', '\\', ':', '*', '?', '\"', '<', '>', '|'])
        .Distinct()
        .ToArray());

    public static string SanitizeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return string.Create(input.Length, input, (span, state) =>
        {
            state.AsSpan().CopyTo(span);
            int index;
            while ((index = span.IndexOfAny(InvalidFileNameChars)) != -1)
            {
                span[index] = '_';
            }
        });
    }

    public static string SanitizeFolderName(string folderName) => SanitizeString(folderName).Trim();
}

public static class CsvUtilities
{
    public static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
