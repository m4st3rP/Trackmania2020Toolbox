using System.Text.RegularExpressions;

namespace Trackmania2020Toolbox;

public partial class InputParser(IConsole console) : IInputParser
{
    [GeneratedRegex(@"\bWeek 0*(\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeeklyShortsRegex();

    [GeneratedRegex(@"\bWeek(?:ly)? Grand 0*(\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeeklyGrandsRegex();

    [GeneratedRegex(@"^(\d+)(?:\.(\d+))?$", RegexOptions.None)]
    private static partial Regex MapRefRegex();

    [GeneratedRegex(@"(Winter|Spring|Summer|Fall)\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonalNameRegex();

    [GeneratedRegex(@"\b(Winter|Spring|Summer|Fall)\s+(\d{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonalFolderRegex();

    [GeneratedRegex(@"^(\d{4})$", RegexOptions.None)]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"^(Winter|Spring|Summer|Fall)\s+(\d{4})(?:\.(\d+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonalRefRegex();

    [GeneratedRegex(@"^(\d{4})[\.\/\-](\d{1,2})[\.\/\-](\d{1,2})$", RegexOptions.None)]
    private static partial Regex ToTdDateFullRegex();

    [GeneratedRegex(@"^(\d{4})[\.\/\-](\d{1,2})$", RegexOptions.None)]
    private static partial Regex ToTdDateMonthRegex();

    [GeneratedRegex(@"^\d{1,2}$", RegexOptions.None)]
    private static partial Regex DayOnlyRegex();

    [GeneratedRegex(@"^(\d{1,2})[\.\/](\d{1,2})$", RegexOptions.None)]
    private static partial Regex MonthDayRegex();

    public int ParseWeeklyShortsNum(string name)
    {
        var match = WeeklyShortsRegex().Match(name);
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    public int ParseWeeklyGrandsNum(string name)
    {
        var match = WeeklyGrandsRegex().Match(name);
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    public List<int> ParseNumbers(string input)
    {
        HashSet<int> result = [];
        ForEachRange(input, [',', ' '], (s, e) =>
        {
            if (e == null)
            {
                if (int.TryParse(s, out var num)) result.Add(num);
            }
            else
            {
                if (int.TryParse(s, out var start) && int.TryParse(e, out var end))
                {
                    for (var i = Math.Min(start, end); i <= Math.Max(start, end); i++) result.Add(i);
                }
            }
        });
        return [.. result.OrderBy(n => n)];
    }

    public List<(MapRef Start, MapRef End)> ParseMapRanges(string input)
    {
        List<(MapRef Start, MapRef End)> ranges = [];
        ForEachRange(input, [',', ' '], (s, e) =>
        {
            var start = ParseMapRef(s);
            if (start == null)
            {
                console.WriteLine($"Error: Could not parse map reference '{s}'.");
                return;
            }

            if (e == null)
            {
                ranges.Add((start, start));
            }
            else
            {
                var end = ParseMapRef(e);
                if (end != null)
                {
                    if (start.CompareTo(end) <= 0) ranges.Add((start, end));
                    else ranges.Add((end, start));
                }
                else
                {
                    console.WriteLine($"Error: Could not parse map reference '{e}'.");
                }
            }
        });
        return ranges;
    }

    public MapRef? ParseMapRef(string s)
    {
        var match = MapRefRegex().Match(s.Trim());
        if (match.Success)
        {
            int camp = int.Parse(match.Groups[1].Value);
            int? map = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : null;
            return new MapRef(camp, map);
        }
        return null;
    }

    public List<(SeasonalRef Start, SeasonalRef End)> ParseSeasonalRanges(string input)
    {
        List<(SeasonalRef Start, SeasonalRef End)> ranges = [];
        ForEachRange(input, [','], (s, e) =>
        {
            var start = ParseSeasonalRef(s);
            if (start == null)
            {
                console.WriteLine($"Error: Could not parse seasonal reference '{s}'.");
                return;
            }

            if (e == null)
            {
                if (YearRegex().IsMatch(s.Trim()))
                {
                    ranges.Add((new SeasonalRef(start.Year, 1), new SeasonalRef(start.Year, 4)));
                }
                else
                {
                    ranges.Add((start, start));
                }
            }
            else
            {
                var end = ParseSeasonalRef(e);
                if (end != null)
                {
                    var finalStart = start;
                    var finalEnd = end;
                    if (YearRegex().IsMatch(s.Trim())) finalStart = new SeasonalRef(start.Year, 1);
                    if (YearRegex().IsMatch(e.Trim())) finalEnd = new SeasonalRef(end.Year, 4);

                    if (finalStart.CompareTo(finalEnd) <= 0) ranges.Add((finalStart, finalEnd));
                    else ranges.Add((finalEnd, finalStart));
                }
                else
                {
                    console.WriteLine($"Error: Could not parse seasonal reference '{e}'.");
                }
            }
        });
        return ranges;
    }

    public SeasonalRef? ParseSeasonalRef(string s)
    {
        s = s.Trim();
        var match = SeasonalRefRegex().Match(s);
        if (match.Success)
        {
            string season = match.Groups[1].Value;
            int year = int.Parse(match.Groups[2].Value);
            int? map = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : null;
            int order = GetSeasonOrder(season);
            return new SeasonalRef(year, order, map);
        }
        match = YearRegex().Match(s);
        if (match.Success)
        {
            int year = int.Parse(match.Groups[1].Value);
            return new SeasonalRef(year, 1);
        }
        return null;
    }

    public SeasonalRef? ParseSeasonalRefFromCampaignName(string campaignName)
    {
        var match = SeasonalNameRegex().Match(campaignName);
        if (match.Success)
        {
            string season = match.Groups[1].Value;
            int year = int.Parse(match.Groups[2].Value);
            int order = GetSeasonOrder(season);
            return new SeasonalRef(year, order);
        }
        match = Regex.Match(campaignName, @"(\d{4})", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            int year = int.Parse(match.Groups[1].Value);
            return new SeasonalRef(year, 1);
        }
        return null;
    }

    public List<(DateTime Start, DateTime End)> ParseToTdRanges(string input, DateTime now)
    {
        List<(DateTime Start, DateTime End)> ranges = [];
        ForEachRange(input, [','], (s, e) =>
        {
            var start = ParseToTdDate(s, now);
            if (!start.HasValue)
            {
                console.WriteLine($"Error: Could not parse date '{s}'.");
                return;
            }

            if (e == null)
            {
                ranges.Add(start.Value);
            }
            else
            {
                var endStr = e.Trim();
                var endDayMatch = DayOnlyRegex().Match(endStr);
                var endMonthDayMatch = MonthDayRegex().Match(endStr);

                if (endDayMatch.Success)
                {
                    if (int.TryParse(endDayMatch.Value, out var endDay) && endDay >= 1)
                    {
                        var end = new DateTime(start.Value.Start.Year, start.Value.Start.Month, Math.Min(DateTime.DaysInMonth(start.Value.Start.Year, start.Value.Start.Month), endDay));
                        if (start.Value.Start <= end) ranges.Add((start.Value.Start, end));
                        else ranges.Add((end, start.Value.Start));
                    }
                    else
                    {
                        console.WriteLine($"Error: Invalid day '{endStr}'.");
                    }
                }
                else if (endMonthDayMatch.Success)
                {
                    if (int.TryParse(endMonthDayMatch.Groups[1].Value, out var endM) &&
                        int.TryParse(endMonthDayMatch.Groups[2].Value, out var endD) &&
                        endM is >= 1 and <= 12 && endD >= 1)
                    {
                        var end = new DateTime(start.Value.Start.Year, endM, 1);
                        end = new DateTime(end.Year, end.Month, Math.Min(DateTime.DaysInMonth(end.Year, end.Month), endD));

                        // Rollover check for month.day
                        if (end < start.Value.Start) end = end.AddYears(1);

                        ranges.Add((start.Value.Start, end));
                    }
                    else
                    {
                        console.WriteLine($"Error: Invalid month or day in '{endStr}'.");
                    }
                }
                else
                {
                    var endResult = ParseToTdDate(endStr, now);
                    if (endResult.HasValue)
                    {
                        var finalStart = start.Value.Start;
                        var finalEnd = endResult.Value.End;

                        // Rollover check for cases like 12.31 - 01.01 (where 01.01 would naturally be same year)
                        if (MonthDayRegex().IsMatch(endStr) && finalEnd < finalStart)
                        {
                            finalEnd = finalEnd.AddYears(1);
                        }

                        if (finalStart <= finalEnd) ranges.Add((finalStart, finalEnd));
                        else ranges.Add((finalEnd, finalStart));
                    }
                    else
                    {
                        console.WriteLine($"Error: Could not parse date '{e}'.");
                    }
                }
            }
        });
        return ranges;
    }

    private (DateTime Start, DateTime End)? ParseToTdDate(string s, DateTime now)
    {
        s = s.Trim();

        if (ToTdDateFullRegex().Match(s) is { Success: true } fullMatch &&
            int.TryParse(fullMatch.Groups[1].Value, out var y) &&
            int.TryParse(fullMatch.Groups[2].Value, out var m) &&
            int.TryParse(fullMatch.Groups[3].Value, out var d) &&
            m is >= 1 and <= 12 && d >= 1)
        {
            int daysInMonth = DateTime.DaysInMonth(y, m);
            var dt = new DateTime(y, m, Math.Min(d, daysInMonth));
            return (dt, dt);
        }

        if (ToTdDateMonthRegex().Match(s) is { Success: true } monthMatch &&
            int.TryParse(monthMatch.Groups[1].Value, out var ym) &&
            int.TryParse(monthMatch.Groups[2].Value, out var mm) &&
            mm is >= 1 and <= 12)
        {
            return (new DateTime(ym, mm, 1), new DateTime(ym, mm, DateTime.DaysInMonth(ym, mm)));
        }

        if (MonthDayRegex().Match(s) is { Success: true } mdMatch &&
            int.TryParse(mdMatch.Groups[1].Value, out var mmd) &&
            int.TryParse(mdMatch.Groups[2].Value, out var dmd) &&
            mmd is >= 1 and <= 12 && dmd >= 1)
        {
            var dt = new DateTime(now.Year, mmd, Math.Min(dmd, DateTime.DaysInMonth(now.Year, mmd)));
            return (dt, dt);
        }

        if (YearRegex().Match(s) is { Success: true } yMatch &&
            int.TryParse(yMatch.Groups[1].Value, out var year))
        {
            return (new DateTime(year, 1, 1), new DateTime(year, 12, 31));
        }

        return null;
    }

    internal static void ForEachRange(string input, char[] separators, Action<string, string?> action)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        var inputSpan = input.AsSpan();
        var sepSearchValues = System.Buffers.SearchValues.Create(separators);

        while (inputSpan.Length > 0)
        {
            int sepIdx = -1;
            int currentOffset = 0;
            var currentSpan = inputSpan;

            // Manual search to correctly handle " - " as a range separator, not a part separator
            while (true)
            {
                int foundIdx = currentSpan.IndexOfAny(sepSearchValues);
                if (foundIdx == -1) break;

                // Check if this separator is actually part of " - "
                if (currentSpan[foundIdx] == ' ' && foundIdx + 2 < currentSpan.Length &&
                    currentSpan[foundIdx + 1] == '-' && currentSpan[foundIdx + 2] == ' ')
                {
                    // This is a range separator, skip it
                    currentSpan = currentSpan[(foundIdx + 3)..];
                    currentOffset += foundIdx + 3;
                    continue;
                }

                sepIdx = currentOffset + foundIdx;
                break;
            }

            ReadOnlySpan<char> part;
            if (sepIdx == -1)
            {
                part = inputSpan;
                inputSpan = ReadOnlySpan<char>.Empty;
            }
            else
            {
                part = inputSpan[..sepIdx];
                inputSpan = inputSpan[(sepIdx + 1)..];
            }

            part = part.Trim();
            if (part.IsEmpty) continue;

            int dashIdx = -1;
            bool isExplicitRange = false;

            // 1. Look for explicit " - " range separator within the part
            for (int i = 0; i < part.Length; i++)
            {
                if (part[i] == '-' && (i > 0 && char.IsWhiteSpace(part[i - 1]) || i < part.Length - 1 && char.IsWhiteSpace(part[i + 1])))
                {
                    dashIdx = i;
                    isExplicitRange = true;
                    break;
                }
            }

            if (isExplicitRange)
            {
                action(part[..dashIdx].Trim().ToString(), part[(dashIdx + 1)..].Trim().ToString());
            }
            else
            {
                // 2. Handle compact range "A-B" while protecting ISO dates
                dashIdx = part.IndexOf('-');
                if (dashIdx != -1 && !IsIsoDate(part))
                {
                    action(part[..dashIdx].Trim().ToString(), part[(dashIdx + 1)..].Trim().ToString());
                }
                else
                {
                    action(part.ToString(), null);
                }
            }
        }
    }

    private static bool IsIsoDate(ReadOnlySpan<char> part)
    {
        if (part.Length < 6) return false;
        if (!char.IsDigit(part[0]) || !char.IsDigit(part[1]) || !char.IsDigit(part[2]) || !char.IsDigit(part[3])) return false;
        if (part[4] != '-') return false;

        int firstDash = 4;
        int secondDash = part[(firstDash + 1)..].IndexOf('-');
        if (secondDash == -1)
        {
            var month = part[(firstDash + 1)..];
            if (month.Length is < 1 or > 2) return false;
            foreach (var c in month) if (!char.IsDigit(c)) return false;
            return true;
        }
        else
        {
            secondDash += firstDash + 1;
            var month = part[(firstDash + 1)..secondDash];
            if (month.Length is < 1 or > 2) return false;
            foreach (var c in month) if (!char.IsDigit(c)) return false;

            var day = part[(secondDash + 1)..];
            if (day.Length is < 1 or > 2) return false;
            foreach (var c in day) if (!char.IsDigit(c)) return false;
            return true;
        }
    }

    public string FormatSeasonalFolderName(string campaignName)
    {
        var match = SeasonalFolderRegex().Match(campaignName);
        if (match.Success)
        {
            string season = match.Groups[1].Value;
            string year = match.Groups[2].Value;
            int order = GetSeasonOrder(season);
            return $"{year} - {order} - {season}";
        }
        return campaignName;
    }

    private static int GetSeasonOrder(string season) => season.ToLower() switch
    {
        "winter" => 1,
        "spring" => 2,
        "summer" => 3,
        "fall" => 4,
        _ => 0
    };
}
