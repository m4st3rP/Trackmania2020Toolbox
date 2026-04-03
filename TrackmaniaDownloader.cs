#:package ManiaAPI.TrackmaniaIO@2.*
#:package TmEssentials@2.*

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ManiaAPI.TrackmaniaIO;
using TmEssentials;

internal static class TrackmaniaDownloader
{
    private static readonly string UserAgent = "TrackmaniaDownloader/1.0 (contact: trackmania-downloader-script@example.com)";
    private static readonly HttpClient HttpClient = new HttpClient();

    public static async Task Main(string[] args)
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        using var tmio = new TrackmaniaIO(UserAgent);

        while (true)
        {
            Console.WriteLine("\nTrackmania Map Downloader");
            Console.WriteLine("1. Weekly Shorts");
            Console.WriteLine("2. Track of the Day");
            Console.WriteLine("Q. Quit");
            Console.Write("Select an option: ");

            var choice = Console.ReadLine()?.Trim().ToUpper();
            if (choice == "1")
            {
                await HandleWeeklyShorts(tmio);
            }
            else if (choice == "2")
            {
                await HandleTrackOfTheDay(tmio);
            }
            else if (choice == "Q")
            {
                break;
            }
            else
            {
                Console.WriteLine("Invalid option.");
            }
        }
    }

    private static async Task HandleWeeklyShorts(TrackmaniaIO tmio)
    {
        Console.WriteLine("\n[Weekly Shorts]");
        Console.WriteLine("Which weeks would you like to download? (e.g., 68, 65-67, 60, 62)");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) return;

        var requestedWeeks = ParseNumbers(input);
        if (!requestedWeeks.Any()) return;

        Console.WriteLine("Fetching available Weekly Shorts campaigns...");
        var campaigns = await FetchAllWeeklyCampaigns(tmio);

        foreach (var weekNum in requestedWeeks)
        {
            var weekSearch = $"Week {weekNum}";
            var campaignItem = campaigns.FirstOrDefault(c =>
            {
                var name = c.Name;
                // Match "Week X" or "Week 0X" exactly within the name to avoid "Week 6" matching "Week 60"
                return System.Text.RegularExpressions.Regex.IsMatch(name, $@"\bWeek 0*{weekNum}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            });

            if (campaignItem == null)
            {
                Console.WriteLine($"Error: Could not find campaign for {weekName}. Skipping.");
                continue;
            }

            var fullCampaign = await tmio.GetWeeklyShortCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null)
            {
                Console.WriteLine($"Could not retrieve playlist for {weekName}.");
                continue;
            }

            var downloadDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Trackmania2020", "Maps", "Downloaded", "Weekly Shorts", weekNum.ToString());

            await DownloadMaps(fullCampaign.Playlist.Select(m => (m.Name, m.FileName, m.FileUrl)), downloadDir);
        }
    }

    private static async Task HandleTrackOfTheDay(TrackmaniaIO tmio)
    {
        Console.WriteLine("\n[Track of the Day]");
        Console.WriteLine("Enter month offset (0 for current, 1 for last month, etc.): ");
        if (!int.TryParse(Console.ReadLine(), out var monthOffset)) monthOffset = 0;

        Console.WriteLine("Which days would you like to download? (e.g., 1, 3-5, 10 or leave empty for all)");
        var dayInput = Console.ReadLine();
        var requestedDays = string.IsNullOrWhiteSpace(dayInput) ? new List<int>() : ParseNumbers(dayInput);

        Console.WriteLine("Fetching TOTD data...");
        var response = await tmio.GetTrackOfTheDaysAsync(monthOffset);
        if (response?.Days == null || !response.Days.Any())
        {
            Console.WriteLine("No TOTD data found.");
            return;
        }

        var year = response.Year;
        var month = response.Month;
        var downloadDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Trackmania2020", "Maps", "Downloaded", "Track of the Day", year.ToString(), month.ToString("D2"));

        var totdDays = response.Days.Where(d => d.Map != null);
        if (requestedDays.Any())
        {
            totdDays = totdDays.Where(d => requestedDays.Contains(d.MonthDay));
        }

        if (!totdDays.Any())
        {
            Console.WriteLine("No matching days found in this month.");
            return;
        }

        Console.WriteLine($"Downloading TOTD for {year}-{month:D2}...");
        var mapsToDownload = totdDays.Select(d => (d.Map.Name, d.Map.FileName, d.Map.FileUrl));

        await DownloadMaps(mapsToDownload, downloadDir);
    }

    private static async Task DownloadMaps(IEnumerable<(string Name, string? FileName, string? FileUrl)> maps, string downloadDir)
    {
        if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);

        foreach (var (name, rawFileName, url) in maps)
        {
            if (string.IsNullOrEmpty(url))
            {
                Console.WriteLine($"No download URL for map: {name}");
                continue;
            }

            var fileName = rawFileName;
            if (string.IsNullOrEmpty(fileName)) fileName = name;

            // Decolor and trim, preserving extension
            fileName = TextFormatter.Deformat(fileName);
            if (fileName.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(0, fileName.Length - 8).Trim() + ".Map.Gbx";
            else if (fileName.EndsWith(".Gbx", StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(0, fileName.Length - 4).Trim() + ".Gbx";
            else
                fileName = fileName.Trim();

            // Clean illegal chars
            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');

            var filePath = Path.Combine(downloadDir, fileName);
            Console.WriteLine($"Downloading {name}...");
            try
            {
                var fileData = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(filePath, fileData);
                Console.WriteLine($"Saved to {filePath}");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download {name}: {ex.Message}");
            }
        }
    }

    private static List<int> ParseNumbers(string input)
    {
        var result = new HashSet<int>();
        var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Contains("-"))
            {
                var range = part.Split('-');
                if (range.Length == 2 && int.TryParse(range[0], out var start) && int.TryParse(range[1], out var end))
                    for (var i = Math.Min(start, end); i <= Math.Max(start, end); i++) result.Add(i);
            }
            else if (int.TryParse(part, out var num)) result.Add(num);
        }
        return result.OrderBy(n => n).ToList();
    }

    private static async Task<List<CampaignItem>> FetchAllWeeklyCampaigns(TrackmaniaIO tmio)
    {
        var allCampaigns = new List<CampaignItem>();
        int page = 0, pageCount = 1;
        while (page < pageCount)
        {
            var response = await tmio.GetWeeklyShortCampaignsAsync(page);
            if (response.Campaigns != null) allCampaigns.AddRange(response.Campaigns);
            pageCount = response.PageCount;
            page++;
        }
        return allCampaigns;
    }
}
