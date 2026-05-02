using System.Text.RegularExpressions;

namespace Trackmania2020Toolbox;

public partial class InputParser(IConsole console)
{
    private readonly IConsole _console = console;

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

    [GeneratedRegex(@"^\d{1,2}[\.\/]\d{1,2}$", RegexOptions.None)]
    private static partial Regex MonthDayRegex();

    [GeneratedRegex(@"\s*-\s*", RegexOptions.None)]
    private static partial Regex RangeSeparatorRegex();

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
        var result = new HashSet<int>();
        var parts = input.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                if (range.Length == 2 && int.TryParse(range[0], out var start) && int.TryParse(range[1], out var end))
                    for (var i = Math.Min(start, end); i <= Math.Max(start, end); i++) result.Add(i);
            }
            else if (int.TryParse(part, out var num)) result.Add(num);
        }
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
                _console.WriteLine($"Error: Could not parse map reference '{s}'.");
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
                    _console.WriteLine($"Error: Could not parse map reference '{e}'.");
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
                _console.WriteLine($"Error: Could not parse seasonal reference '{s}'.");
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
                    _console.WriteLine($"Error: Could not parse seasonal reference '{e}'.");
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
                _console.WriteLine($"Error: Could not parse date '{s}'.");
                return;
            }

            if (e == null)
            {
                ranges.Add(start.Value);
            }
            else
            {
                var endStr = e.Trim();
                if (DayOnlyRegex().IsMatch(endStr) && int.TryParse(endStr, out var endDay))
                {
                    var end = new DateTime(start.Value.Start.Year, start.Value.Start.Month, Math.Min(DateTime.DaysInMonth(start.Value.Start.Year, start.Value.Start.Month), endDay));
                    ranges.Add((start.Value.Start, end));
                }
                else if (MonthDayRegex().IsMatch(endStr))
                {
                    var endParts = endStr.Split(['.', '/']);
                    if (int.TryParse(endParts[0], out var endM) && int.TryParse(endParts[1], out var endD))
                    {
                        var end = new DateTime(start.Value.Start.Year, Math.Min(12, endM), 1);
                        end = new DateTime(end.Year, end.Month, Math.Min(DateTime.DaysInMonth(end.Year, end.Month), endD));

                        // Rollover check for month.day
                        if (end < start.Value.Start) end = end.AddYears(1);

                        ranges.Add((start.Value.Start, end));
                    }
                }
                else
                {
                    var end = ParseToTdDate(endStr, now);
                    if (end.HasValue)
                    {
                        var finalStart = start.Value.Start;
                        var finalEnd = end.Value.End;

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
                        _console.WriteLine($"Error: Could not parse date '{e}'.");
                    }
                }
            }
        });
        return ranges;
    }

    private (DateTime Start, DateTime End)? ParseToTdDate(string s, DateTime now)
    {
        s = s.Trim();
        var match = ToTdDateFullRegex().Match(s);
        if (match.Success)
        {
            int y = int.Parse(match.Groups[1].Value);
            int m = int.Parse(match.Groups[2].Value);
            int d = int.Parse(match.Groups[3].Value);
            if (m is >= 1 and <= 12)
            {
                int daysInMonth = DateTime.DaysInMonth(y, m);
                var dt = new DateTime(y, m, Math.Min(d, daysInMonth));
                return (dt, dt);
            }
        }
        match = ToTdDateMonthRegex().Match(s);
        if (match.Success)
        {
            int y = int.Parse(match.Groups[1].Value);
            int m = int.Parse(match.Groups[2].Value);
            if (m is >= 1 and <= 12)
            {
                return (new DateTime(y, m, 1), new DateTime(y, m, DateTime.DaysInMonth(y, m)));
            }
        }
        match = MonthDayRegex().Match(s);
        if (match.Success)
        {
            var parts = s.Split(['.', '/']);
            if (int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var d))
            {
                if (m is >= 1 and <= 12)
                {
                    var dt = new DateTime(now.Year, m, Math.Min(d, DateTime.DaysInMonth(now.Year, m)));
                    return (dt, dt);
                }
            }
        }
        match = YearRegex().Match(s);
        if (match.Success)
        {
            int y = int.Parse(match.Groups[1].Value);
            return (new DateTime(y, 1, 1), new DateTime(y, 12, 31));
        }
        return null;
    }

    private static void ForEachRange(string input, char[] separators, Action<string, string?> action)
    {
        var parts = input.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            var rangeParts = RangeSeparatorRegex().Split(trimmedPart);
            if (rangeParts.Length == 1) action(rangeParts[0], null);
            else if (rangeParts.Length == 2) action(rangeParts[0], rangeParts[1]);
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
