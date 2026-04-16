#:package GBX.NET@2.*
#:package GBX.NET.LZO@2.*
#:package ManiaAPI.TrackmaniaIO@2.*
#:package TmEssentials@2.*

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    private static string GetScriptDirectory([CallerFilePath] string? path = null) => Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();

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

        if (config.SetGamePath != null)
        {
            SaveGamePath(config.SetGamePath);
            if (!config.Play && config.WeeklyShorts == null && config.WeeklyGrands == null &&
                config.Seasonal == null && config.ClubCampaign == null && config.TotdDate == null &&
                config.ExportMedalsPlayerId == null && !config.ExplicitFolder && config.ExtraPaths.Count == 0)
                return;
        }

        using var tmio = new TrackmaniaIO(UserAgent);

        var mapPaths = new List<string>();
        bool downloadActionTaken = false;

        if (config.WeeklyShorts != null)
        {
            mapPaths.AddRange(await HandleWeeklyShorts(tmio, config.WeeklyShorts, config));
            downloadActionTaken = true;
        }

        if (config.WeeklyGrands != null)
        {
            mapPaths.AddRange(await HandleWeeklyGrands(tmio, config.WeeklyGrands, config));
            downloadActionTaken = true;
        }

        if (config.Seasonal != null)
        {
            mapPaths.AddRange(await HandleSeasonal(tmio, config.Seasonal, config));
            downloadActionTaken = true;
        }

        if (config.ClubCampaign != null)
        {
            mapPaths.AddRange(await HandleClubCampaign(tmio, config.ClubCampaign, config));
            downloadActionTaken = true;
        }

        if (config.TotdDate != null)
        {
            mapPaths.AddRange(await HandleTrackOfTheDay(tmio, config.TotdDate, config));
            downloadActionTaken = true;
        }

        if (config.ExportMedalsPlayerId != null)
        {
            await HandleExportCampaignMedals(tmio, config.ExportMedalsPlayerId, config.ExportMedalsCampaign);
            downloadActionTaken = true;
        }

        // Run fixer for explicit folders, extra paths, or if no downloads were done
        if (config.ExplicitFolder || config.ExtraPaths.Count > 0 || (!downloadActionTaken && config.SetGamePath == null))
        {
            var fixedPaths = RunBatchFixer(config, config.ExtraPaths);
            // Only add paths that aren't already in the list
            foreach (var path in fixedPaths)
            {
                if (!mapPaths.Contains(path)) mapPaths.Add(path);
            }
        }

        if (config.Play)
        {
            LaunchGame(mapPaths);
        }
    }

    private static Config ParseArguments(string[] args)
    {
        string? weeklyShorts = null;
        string? weeklyGrands = null;
        string? seasonal = null;
        string? clubCampaign = null;
        string? totdDate = null;
        string? exportMedalsPlayerId = null;
        string? exportMedalsCampaign = null;
        string folder = DefaultFixerFolder;
        bool explicitFolder = false;
        bool skipTitleUpdate = false;
        bool skipMapTypeConvert = false;
        bool dryRun = false;
        bool force = false;
        bool interactive = true;
        bool play = false;
        string? setGamePath = null;
        var extraPaths = new List<string>();

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
                case "--export-campaign-medals":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        exportMedalsPlayerId = args[++i];
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        {
                            exportMedalsCampaign = args[++i];
                        }
                    }
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
                case "--play":
                    play = true;
                    break;
                case "--set-game-path":
                    if (i + 1 < args.Length) setGamePath = args[++i];
                    break;
                default:
                    if (!args[i].StartsWith("--"))
                    {
                        extraPaths.Add(args[i]);
                    }
                    break;
            }
        }

        return new Config(
            weeklyShorts, weeklyGrands, seasonal, clubCampaign, totdDate,
            exportMedalsPlayerId, exportMedalsCampaign,
            folder, explicitFolder, !skipTitleUpdate, !skipMapTypeConvert, dryRun, force, interactive,
            play, setGamePath, extraPaths
        );
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Trackmania 2020 Toolbox");
        Console.WriteLine("Usage: dotnet run Trackmania2020Toolbox.cs -- [options] [maps/folders...]");
        Console.WriteLine("\nDownload Options:");
        Console.WriteLine("  --weekly-shorts [weeks]    Download Weekly Shorts (e.g., \"68, 70-72\"). Defaults to latest.");
        Console.WriteLine("  --weekly-grands [weeks]    Download Weekly Grands (e.g., \"65\"). Defaults to latest.");
        Console.WriteLine("  --seasonal [name]          Download Seasonal Campaign (e.g., \"Winter 2024\"). Defaults to latest.");
        Console.WriteLine("  --club-campaign <search|id> Download Club Campaign. Searches by name if not <clubId>/<campId>.");
        Console.WriteLine("  --totd [date]              Download Track of the Day. Defaults to today.");
        Console.WriteLine("                             Formats: YYYY-MM, YYYY-MM-DD, YYYY-MM-DD-DD (range)");
        Console.WriteLine("  --export-campaign-medals <PlayerID> [campaign]");
        Console.WriteLine("                             Export official campaign medals to medals.csv.");
        Console.WriteLine("\nPlay Options:");
        Console.WriteLine("  --play                     Launch Trackmania with the maps (requires game running)");
        Console.WriteLine("  --set-game-path <path>     Set the path to Trackmania.exe in config.toml");
        Console.WriteLine("\nOther Options:");
        Console.WriteLine("  --force                    Overwrite existing files");
        Console.WriteLine("  --non-interactive          Disable interactive mode (don't ask for selection)");
        Console.WriteLine("  --folder, -f <path>        Folder for batch fixing (default: Documents\\Trackmania2020\\Maps\\Toolbox)");
        Console.WriteLine("  --skip-title-update        Do not update TitleId (OrbitalDev@falguiere -> TMStadium)");
        Console.WriteLine("  --skip-maptype-convert     Do not convert MapType (TM_Platform -> TM_Race)");
        Console.WriteLine("  --dry-run                  Show changes without saving");
        Console.WriteLine("  --help, -h                 Show this help message");
        Console.WriteLine("\nPositional arguments:");
        Console.WriteLine("  [maps/folders...]          Individual maps or folders to process and/or play");
    }

    private record Config(
        string? WeeklyShorts, string? WeeklyGrands, string? Seasonal, string? ClubCampaign, string? TotdDate,
        string? ExportMedalsPlayerId, string? ExportMedalsCampaign,
        string FolderPath, bool ExplicitFolder, bool UpdateTitle, bool ConvertPlatformMapType, bool DryRun,
        bool ForceOverwrite, bool Interactive, bool Play, string? SetGamePath, List<string> ExtraPaths
    );

    private static string? GetGamePath()
    {
        var configPath = Path.Combine(GetScriptDirectory(), "config.toml");
        if (!File.Exists(configPath)) return null;

        var lines = File.ReadAllLines(configPath);
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("game_path", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    return parts[1].Trim().Trim('"');
                }
            }
        }
        return null;
    }

    private static void SaveGamePath(string path)
    {
        var configPath = Path.Combine(GetScriptDirectory(), "config.toml");
        try
        {
            File.WriteAllText(configPath, $"game_path = \"{path}\"\n");
            Console.WriteLine($"Game path saved to: {configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    private static void LaunchGame(List<string> mapPaths)
    {
        if (mapPaths.Count == 0)
        {
            Console.WriteLine("No maps to play.");
            return;
        }

        var gamePath = GetGamePath();
        if (string.IsNullOrEmpty(gamePath))
        {
            Console.WriteLine("Error: Trackmania.exe path not set.");
            Console.WriteLine("Please set it using: dotnet run Trackmania2020Toolbox.cs -- --set-game-path \"C:\\Path\\To\\Trackmania.exe\"");
            return;
        }

        if (!File.Exists(gamePath))
        {
            Console.WriteLine($"Error: Trackmania.exe not found at {gamePath}");
            return;
        }

        Console.WriteLine("\nNote: Trackmania needs to be already running for this to work correctly.");

        var sortedMaps = mapPaths.Distinct().OrderBy(p => p).ToList();
        var firstMap = sortedMaps.First();
        if (sortedMaps.Count > 1)
        {
            Console.WriteLine($"Note: Drag-and-drop only supports one map. Launching the first one: {Path.GetFileName(firstMap)}");
        }

        var arguments = $"\"{Path.GetFullPath(firstMap)}\"";

        try
        {
            Console.WriteLine($"Launching Trackmania with the map...");
            Process.Start(gamePath, arguments);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching game: {ex.Message}");
        }
    }

    // --- Downloader Logic ---

    private static async Task<List<string>> HandleWeeklyShorts(TrackmaniaIO tmio, string input, Config config)
    {
        var downloadedPaths = new List<string>();
        Console.WriteLine("Fetching available Weekly Shorts campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetWeeklyShortCampaignsAsync(p));

        var requestedWeeks = input.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new List<int> { -1 }
            : ParseNumbers(input);

        if (!requestedWeeks.Any()) return downloadedPaths;

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
            downloadedPaths.AddRange(await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config));
        }
        return downloadedPaths;
    }

    private static async Task<List<string>> HandleWeeklyGrands(TrackmaniaIO tmio, string input, Config config)
    {
        var downloadedPaths = new List<string>();
        Console.WriteLine("Fetching available Weekly Grands campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetWeeklyGrandCampaignsAsync(p));

        var requestedWeeks = input.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new List<int> { -1 }
            : ParseNumbers(input);

        if (!requestedWeeks.Any()) return downloadedPaths;

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
            downloadedPaths.AddRange(await DownloadAndFixMaps(fullCampaign.Playlist.Select(m => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{weekIdStr} - ")), downloadDir, config));
        }
        return downloadedPaths;
    }

    private static async Task<List<string>> HandleSeasonal(TrackmaniaIO tmio, string input, Config config)
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
            return new List<string>();
        }

        Console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

        var fullCampaign = await tmio.GetSeasonalCampaignAsync(campaignItem.Id);
        if (fullCampaign?.Playlist == null) return new List<string>();

        var seasonalFolderName = FormatSeasonalFolderName(campaignItem.Name);
        var downloadDir = Path.Combine(DefaultMapsFolder, "Seasonal", seasonalFolderName);
        return await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
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

    private static async Task<List<string>> HandleClubCampaign(TrackmaniaIO tmio, string input, Config config)
    {
        int clubId, campaignId;
        var parts = input.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out clubId) && int.TryParse(parts[1], out campaignId))
        {
            return await DownloadClubCampaign(tmio, clubId, campaignId, config);
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
                return await DownloadClubCampaign(tmio, match.ClubId ?? 0, match.Id, config);
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
                    return await DownloadClubCampaign(tmio, match.ClubId ?? 0, match.Id, config);
                }
            }
        }
        return new List<string>();
    }

    private static async Task<List<string>> DownloadClubCampaign(TrackmaniaIO tmio, int clubId, int campaignId, Config config)
    {
        var fullCampaign = await tmio.GetClubCampaignAsync(clubId, campaignId);
        if (fullCampaign?.Playlist == null) return new List<string>();

        var clubPart = !string.IsNullOrEmpty(fullCampaign.ClubName) ? fullCampaign.ClubName : clubId.ToString();
        var campaignPart = !string.IsNullOrEmpty(fullCampaign.Name) ? fullCampaign.Name : campaignId.ToString();

        var downloadDir = Path.Combine(DefaultMapsFolder, "Clubs", clubPart, campaignPart);
        return await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
    }

    private static async Task<List<string>> HandleTrackOfTheDay(TrackmaniaIO tmio, string dateInput, Config config)
    {
        var now = DateTime.UtcNow;
        int targetYear = now.Year;
        int targetMonth = now.Month;
        var requestedDays = new List<int>();
        bool isLatest = dateInput.Equals("latest", StringComparison.OrdinalIgnoreCase);

        if (isLatest)
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
        if (response?.Days == null) return new List<string>();

        var totdDays = response.Days.Where(d => d.Map != null);
        if (requestedDays.Any())
        {
            var filteredDays = totdDays.Where(d => requestedDays.Contains(d.MonthDay)).ToList();

            // Fallback for "latest" if today's map is missing and it's before 17:00 UTC
            if (isLatest && !filteredDays.Any() && now.Hour < 17)
            {
                var yesterday = now.AddDays(-1);
                int yesterdayMonthOffset = (now.Year - yesterday.Year) * 12 + (now.Month - yesterday.Month);

                var yesterdayResponse = (yesterdayMonthOffset == monthOffset) ? response : await tmio.GetTrackOfTheDaysAsync(yesterdayMonthOffset);

                if (yesterdayResponse?.Days != null)
                {
                    filteredDays = yesterdayResponse.Days.Where(d => d.Map != null && d.MonthDay == yesterday.Day).ToList();
                    if (filteredDays.Any())
                    {
                        var downloadDirYesterday = Path.Combine(DefaultMapsFolder, "Track of the Day", yesterdayResponse.Year.ToString(), yesterdayResponse.Month.ToString("D2"));
                        return await DownloadAndFixMaps(filteredDays.Select(d => (d.Map!.Name, (string?)d.Map.FileName, (string?)d.Map.FileUrl, (string?)$"{d.MonthDay:D2} - ")), downloadDirYesterday, config);
                    }
                }
            }

            totdDays = filteredDays;
        }

        if (!totdDays.Any()) return new List<string>();

        var downloadDir = Path.Combine(DefaultMapsFolder, "Track of the Day", response.Year.ToString(), response.Month.ToString("D2"));
        return await DownloadAndFixMaps(totdDays.Select(d => (d.Map!.Name, (string?)d.Map.FileName, (string?)d.Map.FileUrl, (string?)$"{d.MonthDay:D2} - ")), downloadDir, config);
    }

    private static async Task<List<string>> DownloadAndFixMaps(IEnumerable<(string Name, string? FileName, string? FileUrl, string? Prefix)> maps, string downloadDir, Config config)
    {
        if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);

        var processedPaths = new List<string>();
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
                processedPaths.Add(filePath);
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

                processedPaths.Add(filePath);
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Failed: {ex.Message}");
            }
        }
        return processedPaths;
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

    private static async Task HandleExportCampaignMedals(TrackmaniaIO tmio, string playerId, string? campaignNameFilter)
    {
        if (!Guid.TryParse(playerId, out var accountId))
        {
            Console.WriteLine("Error: Player ID must be a valid GUID.");
            return;
        }

        string accountIdStr = accountId.ToString();
        Console.WriteLine($"Exporting medals for Player ID: {accountIdStr}");

        Console.WriteLine("Fetching available Seasonal campaigns...");
        var allCampaigns = await FetchAllCampaigns(p => tmio.GetSeasonalCampaignsAsync(p));

        var campaignsToProcess = allCampaigns;
        if (!string.IsNullOrEmpty(campaignNameFilter))
        {
            campaignsToProcess = allCampaigns.Where(c => c.Name.Contains(campaignNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!campaignsToProcess.Any())
            {
                Console.WriteLine($"Error: No seasonal campaigns found matching '{campaignNameFilter}'.");
                return;
            }
        }

        campaignsToProcess = campaignsToProcess.OrderBy(c => c.Id).ToList();

        var csvLines = new List<string> { "Campaign Name, Track Name, Medal, Best Time" };

        for (int i = 0; i < campaignsToProcess.Count; i++)
        {
            var campaignItem = campaignsToProcess[i];
            var deformattedCampaignName = TextFormatter.Deformat(campaignItem.Name);
            Console.WriteLine($"[{i + 1}/{campaignsToProcess.Count}] Campaign: {deformattedCampaignName}");

            await Task.Delay(1000);
            var fullCampaign = await tmio.GetSeasonalCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null)
            {
                Console.WriteLine("  Failed to fetch campaign details.");
                continue;
            }

            foreach (var map in fullCampaign.Playlist)
            {
                await Task.Delay(1000);
                var deformattedMapName = TextFormatter.Deformat(map.Name);
                Console.Write($"  - {deformattedMapName}... ");

                int medal = 0;
                string formattedTime = "00:00:00:000";

                try
                {
                    var leaderboard = await tmio.GetLeaderboardAsync(map.MapUid, accountIdStr);
                    if (leaderboard.Tops != null && leaderboard.Tops.Count > 0)
                    {
                        var record = leaderboard.Tops[0];
                        var t = record.Time;

                        if (t.TotalMilliseconds <= map.AuthorScore.TotalMilliseconds) medal = 4;
                        else if (t.TotalMilliseconds <= map.GoldScore.TotalMilliseconds) medal = 3;
                        else if (t.TotalMilliseconds <= map.SilverScore.TotalMilliseconds) medal = 2;
                        else if (t.TotalMilliseconds <= map.BronzeScore.TotalMilliseconds) medal = 1;

                        formattedTime = $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds:D3}";
                        Console.WriteLine($"Time: {formattedTime}, Medal: {medal}");
                    }
                    else
                    {
                        Console.WriteLine("No record.");
                    }
                }
                catch
                {
                    Console.WriteLine("Error or no record.");
                }

                csvLines.Add($"\"{deformattedCampaignName}\", \"{deformattedMapName}\", {medal}, {formattedTime}");
            }
        }

        try
        {
            File.WriteAllLines("medals.csv", csvLines);
            Console.WriteLine($"\nExport complete! Saved to {Path.GetFullPath("medals.csv")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError saving CSV: {ex.Message}");
        }
    }

    // --- Fixer Logic ---

    private static List<string> RunBatchFixer(Config config, List<string>? extraFiles = null)
    {
        var processedPaths = new List<string>();
        var filesToProcess = new HashSet<string>();

        if (extraFiles != null)
        {
            foreach (var path in extraFiles)
            {
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, FilePattern, SearchOption.AllDirectories))
                        filesToProcess.Add(file);
                }
                else if (File.Exists(path))
                {
                    filesToProcess.Add(path);
                }
            }
        }

        if (config.ExplicitFolder || (extraFiles == null || extraFiles.Count == 0))
        {
            if (Directory.Exists(config.FolderPath))
            {
                foreach (var file in Directory.GetFiles(config.FolderPath, FilePattern, SearchOption.AllDirectories))
                    filesToProcess.Add(file);
            }
        }

        if (filesToProcess.Count == 0) return processedPaths;

        Console.WriteLine($"\nAnalyzing {filesToProcess.Count} maps...");
        int changed = 0;

        foreach (var file in filesToProcess)
        {
            try
            {
                if (ProcessFile(file, config)) changed++;
                processedPaths.Add(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process {file}: {ex.Message}");
            }
        }

        if (config.UpdateTitle || config.ConvertPlatformMapType)
            Console.WriteLine($"\nBatch analysis complete. Analyzed: {filesToProcess.Count}, Updated: {changed}");

        return processedPaths;
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
