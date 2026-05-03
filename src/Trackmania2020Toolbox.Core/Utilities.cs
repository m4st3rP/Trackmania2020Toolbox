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

    public static string SanitizeFolderName(string folderName)
    {
        if (string.IsNullOrEmpty(folderName)) return folderName;

        var span = folderName.AsSpan().Trim();
        if (span.IsEmpty) return string.Empty;

        return string.Create(span.Length, span, (dest, state) =>
        {
            state.CopyTo(dest);
            int index;
            while ((index = dest.IndexOfAny(InvalidFileNameChars)) != -1)
            {
                dest[index] = '_';
            }
        });
    }
}

public static class CsvUtilities
{
    private static readonly SearchValues<char> CsvSpecialChars = SearchValues.Create(",\"\n\r");

    public static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        if (value.AsSpan().IndexOfAny(CsvSpecialChars) == -1)
        {
            return value;
        }

        int quoteCount = 0;
        foreach (char c in value)
        {
            if (c == '"') quoteCount++;
        }

        return string.Create(value.Length + quoteCount + 2, (value, quoteCount), (span, state) =>
        {
            span[0] = '"';
            int destIdx = 1;
            foreach (char c in state.value)
            {
                if (c == '"')
                {
                    span[destIdx++] = '"';
                    span[destIdx++] = '"';
                }
                else
                {
                    span[destIdx++] = c;
                }
            }
            span[destIdx] = '"';
        });
    }
}
