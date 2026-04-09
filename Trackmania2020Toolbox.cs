#:package GBX.NET@2.*
#:package GBX.NET.LZO@2.*
#:package ManiaAPI.TrackmaniaIO@2.*
#:package TmEssentials@2.*

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;
using ManiaAPI.TrackmaniaIO;
using TmEssentials;

internal static class Trackmania2020Toolbox
{
    private static readonly string UserAgent = "Trackmania2020Toolbox/1.0 (contact: trackmania-downloader-script@example.com)";
    private static readonly HttpClient HttpClient = new HttpClient();
    private static readonly string DefaultMapsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Toolbox");
    private static readonly string DefaultFixerFolder = DefaultMapsFolder;
    private const string FilePattern = "*.Map.Gbx";

    public static async Task Main(string[] args)
    {
        Gbx.LZO = new Lzo();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return;
        }

        var config = ParseArguments(args);

        using var tmio = new TrackmaniaIO(UserAgent);

        bool actionTaken = false;

        if (config.WeeklyShorts != null)
        {
            await HandleWeeklyShorts(tmio, config.WeeklyShorts, config);
            actionTaken = true;
        }

        if (config.WeeklyGrands != null)
        {
            await HandleWeeklyGrands(tmio, config.WeeklyGrands, config);
            actionTaken = true;
        }

        if (config.Seasonal != null)
        {
            await HandleSeasonal(tmio, config.Seasonal, config);
            actionTaken = true;
        }

        if (config.ClubCampaign != null)
        {
            await HandleClubCampaign(tmio, config.ClubCampaign, config);
            actionTaken = true;
        }

        if (config.TotdDate != null)
        {
            await HandleTrackOfTheDay(tmio, config.TotdDate, config);
            actionTaken = true;
        }

        // If a folder was explicitly provided or no download actions were taken, run batch fixer
        if (config.ExplicitFolder || !actionTaken)
        {
            RunBatchFixer(config);
        }
    }

    private static Config ParseArguments(string[] args)
    {
        string? weeklyShorts = null;
        string? weeklyGrands = null;
        string? seasonal = null;
        string? clubCampaign = null;
        string? totdDate = null;
        string folder = DefaultFixerFolder;
        bool explicitFolder = false;
        bool skipTitleUpdate = false;
        bool skipMapTypeConvert = false;
        bool dryRun = false;
        bool force = false;
        bool interactive = true;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--weekly-shorts":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) weeklyShorts = args[++i];
                    else weeklyShorts = "latest";
                    break;
                case "--weekly-grands":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) weeklyGrands = args[++i];
                    else weeklyGrands = "latest";
                    break;
                case "--seasonal":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) seasonal = args[++i];
                    else seasonal = "latest";
                    break;
                case "--club-campaign":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) clubCampaign = args[++i];
                    break;
                case "--totd":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) totdDate = args[++i];
                    else totdDate = "latest";
                    break;
                case "--folder":
                case "-f":
                    if (i + 1 < args.Length)
                    {
                        folder = args[++i];
                        explicitFolder = true;
                    }
                    break;
                case "--skip-title-update":
                    skipTitleUpdate = true;
                    break;
                case "--skip-maptype-convert":
                    skipMapTypeConvert = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--non-interactive":
                    interactive = false;
                    break;
            }
        }

        return new Config(
            weeklyShorts, weeklyGrands, seasonal, clubCampaign, totdDate,
            folder, explicitFolder, !skipTitleUpdate, !skipMapTypeConvert, dryRun, force, interactive
        );
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Trackmania 2020 Toolbox");
        Console.WriteLine("Usage: dotnet run Trackmania2020Toolbox.cs -- [options]");
        Console.WriteLine("\nDownload Options:");
        Console.WriteLine("  --weekly-shorts [weeks]    Download Weekly Shorts (e.g., \"68, 70-72\"). Defaults to latest.");
        Console.WriteLine("  --weekly-grands [weeks]    Download Weekly Grands (e.g., \"65\"). Defaults to latest.");
        Console.WriteLine("  --seasonal [name]          Download Seasonal Campaign (e.g., \"Winter 2024\"). Defaults to latest.");
        Console.WriteLine("  --club-campaign <search|id> Download Club Campaign. Searches by name if not <clubId>/<campId>.");
        Console.WriteLine("  --totd [date]              Download Track of the Day. Defaults to today.");
        Console.WriteLine("                             Formats: YYYY-MM, YYYY-MM-DD, YYYY-MM-DD-DD (range)");
        Console.WriteLine("\nOther Options:");
        Console.WriteLine("  --force                    Overwrite existing files");
        Console.WriteLine("  --non-interactive          Disable interactive mode (don't ask for selection)");
        Console.WriteLine("  --folder, -f <path>        Folder for batch fixing (default: Documents\\Trackmania2020\\Maps\\Toolbox)");
        Console.WriteLine("  --skip-title-update        Do not update TitleId (OrbitalDev@falguiere -> TMStadium)");
        Console.WriteLine("  --skip-maptype-convert     Do not convert MapType (TM_Platform -> TM_Race)");
        Console.WriteLine("  --dry-run                  Show changes without saving");
        Console.WriteLine("  --help, -h                 Show this help message");
    }

    private record Config(
        string? WeeklyShorts, string? WeeklyGrands, string? Seasonal, string? ClubCampaign, string? TotdDate,
        string FolderPath, bool ExplicitFolder, bool UpdateTitle, bool ConvertPlatformMapType, bool DryRun,
        bool ForceOverwrite, bool Interactive
    );

    // --- Downloader Logic ---

    private static async Task HandleWeeklyShorts(TrackmaniaIO tmio, string input, Config config)
    {
        Console.WriteLine("Fetching available Weekly Shorts campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetWeeklyShortCampaignsAsync(p));

        var requestedWeeks = input.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new List<int> { -1 }
            : ParseNumbers(input);

        if (!requestedWeeks.Any()) return;

        foreach (var weekNum in requestedWeeks)
        {
            CampaignItem? campaignItem;
            if (weekNum == -1)
            {
                campaignItem = campaigns.OrderByDescending(c => c.Id).FirstOrDefault();
            }
            else
            {
                campaignItem = campaigns.FirstOrDefault(c => Regex.IsMatch(c.Name, $@"\bWeek 0*{weekNum}\b", RegexOptions.IgnoreCase));
            }

            if (campaignItem == null)
            {
                Console.WriteLine($"Error: Could not find campaign {(weekNum == -1 ? "latest" : $"Week {weekNum}")}. Skipping.");
                continue;
            }

            Console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

            var fullCampaign = await tmio.GetWeeklyShortCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var weekIdStr = weekNum == -1 ? Regex.Match(campaignItem.Name, @"\bWeek 0*(\d+)\b", RegexOptions.IgnoreCase).Groups[1].Value : weekNum.ToString();
            if (string.IsNullOrEmpty(weekIdStr)) weekIdStr = campaignItem.Id.ToString();

            var downloadDir = Path.Combine(DefaultMapsFolder, "Weekly Shorts", weekIdStr);
            await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
        }
    }

    private static async Task HandleWeeklyGrands(TrackmaniaIO tmio, string input, Config config)
    {
        Console.WriteLine("Fetching available Weekly Grands campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetWeeklyGrandCampaignsAsync(p));

        var requestedWeeks = input.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new List<int> { -1 }
            : ParseNumbers(input);

        if (!requestedWeeks.Any()) return;

        foreach (var weekNum in requestedWeeks)
        {
            CampaignItem? campaignItem;
            if (weekNum == -1)
            {
                campaignItem = campaigns.OrderByDescending(c => c.Id).FirstOrDefault();
            }
            else
            {
                campaignItem = campaigns.FirstOrDefault(c => Regex.IsMatch(c.Name, $@"\bWeek Grand 0*{weekNum}\b", RegexOptions.IgnoreCase));
            }

            if (campaignItem == null)
            {
                Console.WriteLine($"Error: Could not find campaign {(weekNum == -1 ? "latest" : $"Week Grand {weekNum}")}. Skipping.");
                continue;
            }

            Console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

            var fullCampaign = await tmio.GetWeeklyGrandCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var weekIdStr = weekNum == -1 ? Regex.Match(campaignItem.Name, @"\bWeek Grand 0*(\d+)\b", RegexOptions.IgnoreCase).Groups[1].Value : weekNum.ToString();
            if (string.IsNullOrEmpty(weekIdStr)) weekIdStr = campaignItem.Id.ToString();

            var downloadDir = Path.Combine(DefaultMapsFolder, "Weekly Grands");
            await DownloadAndFixMaps(fullCampaign.Playlist.Select(m => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{weekIdStr} - ")), downloadDir, config);
        }
    }

    private static async Task HandleSeasonal(TrackmaniaIO tmio, string input, Config config)
    {
        Console.WriteLine("Fetching available Seasonal campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetSeasonalCampaignsAsync(p));

        CampaignItem? campaignItem;
        if (input.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            campaignItem = campaigns.OrderByDescending(c => c.Id).FirstOrDefault();
        }
        else
        {
            campaignItem = campaigns.FirstOrDefault(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase));
        }

        if (campaignItem == null)
        {
            Console.WriteLine($"Error: Could not find seasonal campaign matching '{input}'.");
            return;
        }

        Console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

        var fullCampaign = await tmio.GetSeasonalCampaignAsync(campaignItem.Id);
        if (fullCampaign?.Playlist == null) return;

        var seasonalFolderName = FormatSeasonalFolderName(campaignItem.Name);
        var downloadDir = Path.Combine(DefaultMapsFolder, "Seasonal", seasonalFolderName);
        await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
    }

    private static string FormatSeasonalFolderName(string campaignName)
    {
        var match = Regex.Match(campaignName, @"(Winter|Spring|Summer|Fall)\s+(\d{4})", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            string season = match.Groups[1].Value;
            string year = match.Groups[2].Value;
            int order = season.ToLower() switch
            {
                "winter" => 1,
                "spring" => 2,
                "summer" => 3,
                "fall" => 4,
                _ => 0
            };
            return $"{year} - {order} - {season}";
        }
        return campaignName;
    }

    private static async Task HandleClubCampaign(TrackmaniaIO tmio, string input, Config config)
    {
        int clubId, campaignId;
        var parts = input.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out clubId) && int.TryParse(parts[1], out campaignId))
        {
            await DownloadClubCampaign(tmio, clubId, campaignId, config);
        }
        else
        {
            Console.WriteLine($"Searching for club campaigns matching '{input}'...");
            var campaigns = await FetchAllCampaigns(p => tmio.GetClubCampaignsAsync(p));
            var matches = campaigns.Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0)
            {
                Console.WriteLine("No matching club campaigns found.");
            }
            else if (matches.Count == 1 || !config.Interactive)
            {
                var match = matches[0];
                Console.WriteLine($"Found: {TextFormatter.Deformat(match.Name)} (ID: {match.ClubId}/{match.Id})");
                await DownloadClubCampaign(tmio, match.ClubId ?? 0, match.Id, config);
            }
            else
            {
                Console.WriteLine("\nMultiple campaigns found:");
                for (int i = 0; i < matches.Count; i++)
                {
                    Console.WriteLine($"{i + 1}: {TextFormatter.Deformat(matches[i].Name)} (ID: {matches[i].ClubId}/{matches[i].Id})");
                }
                Console.Write("\nSelect a campaign number (or 0 to cancel): ");
                if (int.TryParse(Console.ReadLine(), out var choice) && choice > 0 && choice <= matches.Count)
                {
                    var match = matches[choice - 1];
                    await DownloadClubCampaign(tmio, match.ClubId ?? 0, match.Id, config);
                }
            }
        }
    }

    private static async Task DownloadClubCampaign(TrackmaniaIO tmio, int clubId, int campaignId, Config config)
    {
        var fullCampaign = await tmio.GetClubCampaignAsync(clubId, campaignId);
        if (fullCampaign?.Playlist == null) return;

        var clubPart = !string.IsNullOrEmpty(fullCampaign.ClubName) ? fullCampaign.ClubName : clubId.ToString();
        var campaignPart = !string.IsNullOrEmpty(fullCampaign.Name) ? fullCampaign.Name : campaignId.ToString();

        var downloadDir = Path.Combine(DefaultMapsFolder, "Clubs", clubPart, campaignPart);
        await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
    }

    private static async Task HandleTrackOfTheDay(TrackmaniaIO tmio, string dateInput, Config config)
    {
        var now = DateTime.UtcNow;
        int targetYear = now.Year;
        int targetMonth = now.Month;
        var requestedDays = new List<int>();

        if (dateInput.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            requestedDays.Add(now.Day);
        }
        else
        {
            var parts = dateInput.Split(new[] { '-', '/', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m))
                {
                    targetYear = y;
                    targetMonth = m;

                    if (parts.Length == 2)
                    {
                        // Whole month: requestedDays stays empty, we'll handle it later
                    }
                    else if (parts.Length == 3)
                    {
                        // Single day: YYYY-MM-DD
                        if (int.TryParse(parts[2], out var d)) requestedDays.Add(d);
                    }
                    else if (parts.Length == 4)
                    {
                        // Range: YYYY-MM-DD-DD
                        if (int.TryParse(parts[2], out var dStart) && int.TryParse(parts[3], out var dEnd))
                        {
                            for (int d = Math.Min(dStart, dEnd); d <= Math.Max(dStart, dEnd); d++)
                                requestedDays.Add(d);
                        }
                    }
                }
            }
        }

        int monthOffset = (now.Year - targetYear) * 12 + (now.Month - targetMonth);
        var response = await tmio.GetTrackOfTheDaysAsync(monthOffset);
        if (response?.Days == null) return;

        var downloadDir = Path.Combine(DefaultMapsFolder, "Track of the Day", response.Year.ToString(), response.Month.ToString("D2"));
        var totdDays = response.Days.Where(d => d.Map != null);
        if (requestedDays.Any()) totdDays = totdDays.Where(d => requestedDays.Contains(d.MonthDay));

        await DownloadAndFixMaps(totdDays.Select(d => (d.Map!.Name, (string?)d.Map.FileName, (string?)d.Map.FileUrl, (string?)$"{d.MonthDay:D2} - ")), downloadDir, config);
    }

    private static async Task DownloadAndFixMaps(IEnumerable<(string Name, string? FileName, string? FileUrl, string? Prefix)> maps, string downloadDir, Config config)
    {
        if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);

        var mapList = maps.ToList();
        Console.WriteLine($"Processing {mapList.Count} maps...");

        for (int i = 0; i < mapList.Count; i++)
        {
            var (name, rawFileName, url, prefix) = mapList[i];
            if (string.IsNullOrEmpty(url)) continue;

            var fileName = rawFileName ?? name;
            var deformattedName = TextFormatter.Deformat(name);
            fileName = TextFormatter.Deformat(fileName);
            if (fileName.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase)) fileName = fileName.Substring(0, fileName.Length - 8).Trim() + ".Map.Gbx";
            else if (fileName.EndsWith(".Gbx", StringComparison.OrdinalIgnoreCase)) fileName = fileName.Substring(0, fileName.Length - 4).Trim() + ".Gbx";
            else fileName = fileName.Trim();

            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');

            if (!string.IsNullOrEmpty(prefix)) fileName = prefix + fileName;

            var filePath = Path.Combine(downloadDir, fileName);
            Console.Write($"[{i + 1}/{mapList.Count}] {deformattedName}... ");

            if (File.Exists(filePath) && !config.ForceOverwrite)
            {
                Console.WriteLine("Skipped (already exists)");
                continue;
            }

            try
            {
                var fileData = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(filePath, fileData);
                Console.Write("Downloaded and ");

                // Fix the map immediately
                if (ProcessFile(filePath, config)) Console.WriteLine("Fixed.");
                else Console.WriteLine("Saved.");

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Failed: {ex.Message}");
            }
        }
    }

    private static async Task<List<CampaignItem>> FetchAllCampaigns(Func<int, Task<CampaignCollection>> fetchFunc)
    {
        var all = new List<CampaignItem>();
        int page = 0, pageCount = 1;
        while (page < pageCount)
        {
            var response = await fetchFunc(page);
            if (response.Campaigns != null) all.AddRange(response.Campaigns);
            pageCount = response.PageCount;
            page++;
        }
        return all;
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

    // --- Fixer Logic ---

    private static void RunBatchFixer(Config config)
    {
        if (!config.UpdateTitle && !config.ConvertPlatformMapType) return;

        Console.WriteLine($"\nRunning batch fixer on: {config.FolderPath}");
        if (!Directory.Exists(config.FolderPath))
        {
            Console.WriteLine($"Directory does not exist: {config.FolderPath}");
            return;
        }

        var files = Directory.GetFiles(config.FolderPath, FilePattern, SearchOption.AllDirectories);
        int analyzed = 0, changed = 0;

        foreach (var file in files)
        {
            try
            {
                if (ProcessFile(file, config)) changed++;
                analyzed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nBatch analysis complete. Analyzed: {analyzed}, Updated: {changed}");
    }

    private static bool ProcessFile(string filePath, Config cfg)
    {
        if (!cfg.UpdateTitle && !cfg.ConvertPlatformMapType) return false;

        var gbx = Gbx.Parse<CGameCtnChallenge>(filePath);
        var map = gbx.Node;
        bool changed = false;

        if (cfg.UpdateTitle && map.TitleId == "OrbitalDev@falguiere")
        {
            map.TitleId = "TMStadium";
            changed = true;
        }

        if (cfg.ConvertPlatformMapType && map.MapType == "TrackMania\\TM_Platform")
        {
            map.MapType = "TrackMania\\TM_Race";
            changed = true;
        }

        if (changed)
        {
            var fileName = Path.GetFileName(filePath);
            var deformattedFileName = TextFormatter.Deformat(fileName);

            if (cfg.DryRun) Console.WriteLine($"  [Dry Run] Would update: {deformattedFileName}");
            else
            {
                gbx.Save(filePath);
                Console.WriteLine($"  Updated: {deformattedFileName}");
            }
        }

        return changed;
    }
}
