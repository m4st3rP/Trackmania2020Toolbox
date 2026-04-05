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
    private static readonly string DefaultFixerFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps");
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

        if (config.Club != null)
        {
            await HandleClub(tmio, config.Club, config);
            actionTaken = true;
        }

        if (config.TotdDate != null)
        {
            await HandleTrackOfTheDay(tmio, config.TotdDate, config.TotdDays, config);
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
        string? club = null;
        string? totdDate = null;
        string? totdDays = null;
        string folder = DefaultFixerFolder;
        bool explicitFolder = false;
        bool skipTitleUpdate = false;
        bool skipMapTypeConvert = false;
        bool dryRun = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--weekly-shorts":
                    if (i + 1 < args.Length) weeklyShorts = args[++i];
                    break;
                case "--weekly-grands":
                    if (i + 1 < args.Length) weeklyGrands = args[++i];
                    break;
                case "--seasonal":
                    if (i + 1 < args.Length) seasonal = args[++i];
                    break;
                case "--club":
                    if (i + 1 < args.Length) club = args[++i];
                    break;
                case "--totd":
                    if (i + 1 < args.Length) totdDate = args[++i];
                    if (i + 1 < args.Length && !args[i+1].StartsWith("--")) totdDays = args[++i];
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
            }
        }

        return new Config(
            weeklyShorts, weeklyGrands, seasonal, club, totdDate, totdDays,
            folder, explicitFolder, !skipTitleUpdate, !skipMapTypeConvert, dryRun
        );
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Trackmania 2020 Toolbox");
        Console.WriteLine("Usage: dotnet run Trackmania2020Toolbox.cs -- [options]");
        Console.WriteLine("\nDownload Options:");
        Console.WriteLine("  --weekly-shorts <weeks>    Download Weekly Shorts (e.g., \"68, 70-72\")");
        Console.WriteLine("  --weekly-grands <weeks>    Download Weekly Grands (e.g., \"65\")");
        Console.WriteLine("  --seasonal <name>          Download Seasonal Campaign (e.g., \"Winter 2024\")");
        Console.WriteLine("  --club <clubId>/<campId>   Download Club Campaign (e.g., \"123/456\")");
        Console.WriteLine("  --totd <YYYY-MM> [days]    Download Track of the Day (e.g., \"2024-10\" \"1-5\")");
        Console.WriteLine("\nFixer Options (applied to downloads by default):");
        Console.WriteLine("  --folder, -f <path>        Folder for batch fixing (default: Documents\\Trackmania2020\\Maps)");
        Console.WriteLine("  --skip-title-update        Do not update TitleId (OrbitalDev@falguiere -> TMStadium)");
        Console.WriteLine("  --skip-maptype-convert     Do not convert MapType (TM_Platform -> TM_Race)");
        Console.WriteLine("  --dry-run                  Show changes without saving");
        Console.WriteLine("  --help, -h                 Show this help message");
    }

    private record Config(
        string? WeeklyShorts, string? WeeklyGrands, string? Seasonal, string? Club, string? TotdDate, string? TotdDays,
        string FolderPath, bool ExplicitFolder, bool UpdateTitle, bool ConvertPlatformMapType, bool DryRun
    );

    // --- Downloader Logic ---

    private static async Task HandleWeeklyShorts(TrackmaniaIO tmio, string input, Config config)
    {
        var requestedWeeks = ParseNumbers(input);
        if (!requestedWeeks.Any()) return;

        Console.WriteLine("Fetching available Weekly Shorts campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetWeeklyShortCampaignsAsync(p));

        foreach (var weekNum in requestedWeeks)
        {
            var weekName = $"Week {weekNum}";
            var campaignItem = campaigns.FirstOrDefault(c => Regex.IsMatch(c.Name, $@"\bWeek 0*{weekNum}\b", RegexOptions.IgnoreCase));

            if (campaignItem == null)
            {
                Console.WriteLine($"Error: Could not find campaign for {weekName}. Skipping.");
                continue;
            }

            var fullCampaign = await tmio.GetWeeklyShortCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Downloaded", "Weekly Shorts", weekNum.ToString());
            await DownloadAndFixMaps(fullCampaign.Playlist.Select(m => (m.Name, (string?)m.FileName, (string?)m.FileUrl)), downloadDir, config);
        }
    }

    private static async Task HandleWeeklyGrands(TrackmaniaIO tmio, string input, Config config)
    {
        var requestedWeeks = ParseNumbers(input);
        if (!requestedWeeks.Any()) return;

        Console.WriteLine("Fetching available Weekly Grands campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetWeeklyGrandCampaignsAsync(p));

        foreach (var weekNum in requestedWeeks)
        {
            var weekName = $"Week Grand {weekNum}";
            var campaignItem = campaigns.FirstOrDefault(c => Regex.IsMatch(c.Name, $@"\bWeek Grand 0*{weekNum}\b", RegexOptions.IgnoreCase));

            if (campaignItem == null)
            {
                Console.WriteLine($"Error: Could not find campaign for {weekName}. Skipping.");
                continue;
            }

            var fullCampaign = await tmio.GetWeeklyGrandCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Downloaded", "Weekly Grands");
            await DownloadAndFixMaps(fullCampaign.Playlist.Select(m => (m.Name, (string?)m.FileName, (string?)m.FileUrl)), downloadDir, config, prefix: $"{weekNum} - ");
        }
    }

    private static async Task HandleSeasonal(TrackmaniaIO tmio, string input, Config config)
    {
        Console.WriteLine("Fetching available Seasonal campaigns...");
        var campaigns = await FetchAllCampaigns(p => tmio.GetSeasonalCampaignsAsync(p));

        var campaignItem = campaigns.FirstOrDefault(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase));
        if (campaignItem == null)
        {
            Console.WriteLine($"Error: Could not find seasonal campaign matching '{input}'.");
            return;
        }

        var fullCampaign = await tmio.GetSeasonalCampaignAsync(campaignItem.Id);
        if (fullCampaign?.Playlist == null) return;

        var downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Downloaded", "Seasonal", campaignItem.Name);
        var mapsWithPrefix = fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, prefix: $"{(i + 1):D2} - ")).ToList();
        await DownloadAndFixMaps(mapsWithPrefix.Select(x => (x.Name, x.Item2, x.Item3)), downloadDir, config, mapsWithPrefix.Select(x => x.prefix).ToList());
    }

    private static async Task HandleClub(TrackmaniaIO tmio, string input, Config config)
    {
        var parts = input.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var clubId) || !int.TryParse(parts[1], out var campaignId))
        {
            Console.WriteLine("Invalid club format. Use <clubId>/<campaignId>.");
            return;
        }

        var fullCampaign = await tmio.GetClubCampaignAsync(clubId, campaignId);
        if (fullCampaign?.Playlist == null) return;

        var clubPart = !string.IsNullOrEmpty(fullCampaign.ClubName) ? fullCampaign.ClubName : clubId.ToString();
        var campaignPart = !string.IsNullOrEmpty(fullCampaign.Name) ? fullCampaign.Name : campaignId.ToString();

        var downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Downloaded", "Clubs", clubPart, campaignPart);
        var mapsWithPrefix = fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, prefix: $"{(i + 1):D2} - ")).ToList();
        await DownloadAndFixMaps(mapsWithPrefix.Select(x => (x.Name, x.Item2, x.Item3)), downloadDir, config, mapsWithPrefix.Select(x => x.prefix).ToList());
    }

    private static async Task HandleTrackOfTheDay(TrackmaniaIO tmio, string dateInput, string? dayInput, Config config)
    {
        int monthOffset = 0;
        var dateParts = dateInput.Split(new[] { '-', '/', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (dateParts.Length == 2 && int.TryParse(dateParts[0], out var year) && int.TryParse(dateParts[1], out var month))
        {
            var now = DateTime.UtcNow;
            monthOffset = (now.Year - year) * 12 + (now.Month - month);
        }

        var requestedDays = string.IsNullOrWhiteSpace(dayInput) ? new List<int>() : ParseNumbers(dayInput);

        var response = await tmio.GetTrackOfTheDaysAsync(monthOffset);
        if (response?.Days == null) return;

        var downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Downloaded", "Track of the Day", response.Year.ToString(), response.Month.ToString("D2"));
        var totdDays = response.Days.Where(d => d.Map != null);
        if (requestedDays.Any()) totdDays = totdDays.Where(d => requestedDays.Contains(d.MonthDay));

        await DownloadAndFixMaps(totdDays.Select(d => (d.Map!.Name, (string?)d.Map.FileName, (string?)d.Map.FileUrl)), downloadDir, config);
    }

    private static async Task DownloadAndFixMaps(IEnumerable<(string Name, string? FileName, string? FileUrl)> maps, string downloadDir, Config config, List<string>? prefixes = null, string? prefix = null)
    {
        if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);

        var mapList = maps.ToList();
        for (int i = 0; i < mapList.Count; i++)
        {
            var (name, rawFileName, url) = mapList[i];
            if (string.IsNullOrEmpty(url)) continue;

            var fileName = rawFileName ?? name;
            fileName = TextFormatter.Deformat(fileName);
            if (fileName.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase)) fileName = fileName.Substring(0, fileName.Length - 8).Trim() + ".Map.Gbx";
            else if (fileName.EndsWith(".Gbx", StringComparison.OrdinalIgnoreCase)) fileName = fileName.Substring(0, fileName.Length - 4).Trim() + ".Gbx";
            else fileName = fileName.Trim();

            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');

            if (prefixes != null && i < prefixes.Count) fileName = prefixes[i] + fileName;
            else if (!string.IsNullOrEmpty(prefix)) fileName = prefix + fileName;

            var filePath = Path.Combine(downloadDir, fileName);
            Console.WriteLine($"Downloading {name}...");
            try
            {
                var fileData = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(filePath, fileData);
                Console.WriteLine($"Saved to {filePath}");

                // Fix the map immediately
                ProcessFile(filePath, config);

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process {name}: {ex.Message}");
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
            if (cfg.DryRun) Console.WriteLine($"  [Dry Run] Would update: {Path.GetFileName(filePath)}");
            else
            {
                gbx.Save(filePath);
                Console.WriteLine($"  Updated: {Path.GetFileName(filePath)}");
            }
        }

        return changed;
    }
}
