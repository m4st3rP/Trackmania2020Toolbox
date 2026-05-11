using System.Buffers;
using TmEssentials;

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

        var span = input.AsSpan();
        if (span.IndexOfAny(InvalidFileNameChars) == -1) return input;

        return string.Create(input.Length, input, (dest, state) =>
        {
            for (int i = 0; i < state.Length; i++)
            {
                char c = state[i];
                dest[i] = InvalidFileNameChars.Contains(c) ? '_' : c;
            }
        });
    }

    public static string SanitizeFolderName(string folderName)
    {
        if (string.IsNullOrEmpty(folderName)) return folderName;

        var deformatted = TextFormatter.Deformat(folderName);
        var trimmed = deformatted.Trim();
        if (trimmed.Length == 0) return string.Empty;

        var span = trimmed.AsSpan();
        if (span.IndexOfAny(InvalidFileNameChars) == -1) return trimmed;

        return string.Create(trimmed.Length, trimmed, (dest, state) =>
        {
            for (int i = 0; i < state.Length; i++)
            {
                char c = state[i];
                dest[i] = InvalidFileNameChars.Contains(c) ? '_' : c;
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
