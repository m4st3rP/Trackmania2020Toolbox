using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using TmEssentials;

namespace Trackmania2020Toolbox;

public class ToolboxApp(ITrackmaniaApi api, IFileSystem fs, INetworkService net, IMapFixer fixer, IConsole console, IDateTime dateTime, string scriptDirectory, IInputParser parser, IMapDownloader downloader, IConfigService? configService = null)
{
    private const int TotdReleaseHour = 17;

    private readonly ITrackmaniaApi _api = api;
    private readonly IFileSystem _fs = fs;
    private readonly INetworkService _net = net;
    private readonly IMapFixer _fixer = fixer;
    private readonly IConsole _console = console;
    private readonly IDateTime _dateTime = dateTime;
    private readonly IConfigService _configService = configService ?? new RealConfigService(fs);
    private readonly string _scriptDirectory = scriptDirectory;
    private readonly string _defaultMapsFolder = Config.DefaultMapsFolder;

    private readonly IInputParser _parser = parser;
    private readonly IMapDownloader _downloader = downloader;

    public string ScriptDirectory => _scriptDirectory;
    public string DefaultMapsFolder => _defaultMapsFolder;

    public ITrackmaniaApi Api => _api;

    public async Task RunAsync(Config config, CancellationToken ct = default)
    {
        var appCfg = config.App;
        var dlCfg = config.Downloader;
        var tmxCfg = config.Tmx;
        var fixerCfg = config.Fixer;

        if (appCfg.SetGamePath != null)
        {
            await SaveGamePathAsync(appCfg.SetGamePath, ct);
            if (!appCfg.Play && dlCfg.WeeklyShorts == null && dlCfg.WeeklyGrands == null &&
                dlCfg.Seasonal == null && dlCfg.ClubCampaign == null && dlCfg.ToTDDate == null &&
                dlCfg.ExportMedalsPlayerId == null && !fixerCfg.ExplicitFolder && appCfg.ExtraPaths.Count == 0)
                return;
        }

        HashSet<string> mapPaths = [];
        bool downloadActionTaken = false;

        var downloadHandlers = new List<(bool Condition, Func<CancellationToken, Task<List<string>>> Action)>
        {
            (dlCfg.WeeklyShorts != null, (c) => HandleWeeklyShortsAsync(dlCfg.WeeklyShorts!, config, c)),
            (dlCfg.WeeklyGrands != null, (c) => HandleWeeklyGrandsAsync(dlCfg.WeeklyGrands!, config, c)),
            (dlCfg.Seasonal != null, (c) => HandleSeasonalAsync(dlCfg.Seasonal!, config, c)),
            (dlCfg.ClubCampaign != null, (c) => HandleClubCampaignAsync(dlCfg.ClubCampaign!, config, c)),
            (dlCfg.ToTDDate != null, (c) => HandleTrackOfTheDayAsync(dlCfg.ToTDDate!, config, c)),
            (tmxCfg.TmxMaps != null, (c) => HandleTmxMapsAsync(tmxCfg.TmxMaps!, config, c)),
            (tmxCfg.TmxPacks != null, (c) => HandleTmxPacksAsync(tmxCfg.TmxPacks!, config, c)),
            (tmxCfg.TmxSearch != null || tmxCfg.TmxAuthor != null, (c) => HandleTmxSearchAsync(tmxCfg.TmxSearch, tmxCfg.TmxAuthor, tmxCfg.TmxSort, tmxCfg.TmxDesc, config, c)),
            (tmxCfg.TmxRandom, (c) => HandleTmxRandomAsync(config, c))
        };

        foreach (var (condition, action) in downloadHandlers)
        {
            if (condition)
            {
                foreach (var path in await action(ct)) mapPaths.Add(path);
                downloadActionTaken = true;
            }
        }

        if (dlCfg.ExportMedalsPlayerId != null)
        {
            await HandleExportCampaignMedalsAsync(dlCfg.ExportMedalsPlayerId, dlCfg.ExportMedalsCampaign, config, dlCfg.ExportMedalsOutputPath, ct);
            downloadActionTaken = true;
        }

        if (fixerCfg.ExplicitFolder || appCfg.ExtraPaths.Count > 0 || (!downloadActionTaken && appCfg.SetGamePath == null))
        {
            var fixedPaths = await RunBatchFixerAsync(config, appCfg.ExtraPaths, ct);
            foreach (var path in fixedPaths) mapPaths.Add(path);
        }

        if (appCfg.Play)
        {
            await LaunchGameAsync([.. mapPaths], config, ct);
        }
    }

    private async Task SaveGamePathAsync(string path, CancellationToken ct)
    {
        try
        {
            var config = await _configService.LoadConfigAsync(_scriptDirectory, ct);
            config.App.SetGamePath = path;
            await _configService.SaveConfigAsync(_scriptDirectory, config, ct);
            _console.WriteLine($"Game path saved to: {Path.Combine(_scriptDirectory, "config.toml")}");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    public async Task LaunchGameAsync(List<string> mapPaths, Config? config = null, CancellationToken ct = default)
    {
        if (mapPaths.Count == 0)
        {
            _console.WriteLine("No maps to play.");
            return;
        }

        config ??= await _configService.LoadConfigAsync(_scriptDirectory, ct);
        var gamePath = config.App.SetGamePath;
        if (string.IsNullOrEmpty(gamePath))
        {
            _console.WriteLine("Error: Trackmania.exe path not set.");
            _console.WriteLine("Please set it in the Settings tab or via CLI using: --set-game-path \"C:\\Path\\To\\Trackmania.exe\"");
            return;
        }

        if (!_fs.FileExists(gamePath))
        {
            _console.WriteLine($"Error: Trackmania.exe not found at: {gamePath}");
            return;
        }

        _console.WriteLine("\nNote: Trackmania needs to be already running for this to work correctly.");

        var sortedMaps = mapPaths.Distinct().OrderBy(p => p).ToList();
        var firstMap = sortedMaps.First();
        if (sortedMaps.Count > 1)
        {
            _console.WriteLine($"Note: Multiple maps selected. Launching only the first one: {Path.GetFileName(firstMap)}");
        }

        if (!_fs.FileExists(firstMap))
        {
            _console.WriteLine($"Error: Map file not found: {firstMap}");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = gamePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(gamePath)
        };
        psi.ArgumentList.Add(Path.GetFullPath(firstMap));

        try
        {
            _console.WriteLine($"Launching Trackmania with the map: {Path.GetFileName(firstMap)}");
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error launching game: {ex.Message}");
        }
    }

    public Task<List<string>> HandleWeeklyShortsAsync(string input, Config config, CancellationToken ct = default) =>
        HandleWeeklyCampaignAsync(input, config, "Weekly Shorts",
            p => _api.GetWeeklyShortCampaignsAsync(p, ct),
            id => _api.GetWeeklyShortCampaignAsync(id, ct),
            c => _parser.ParseWeeklyShortsNum(c.Name),
            weekId => Path.Combine(_defaultMapsFolder, "Weekly Shorts", weekId),
            (m, i, weekId) => $"{(i + 1):D2} - ", ct);

    public Task<List<string>> HandleWeeklyGrandsAsync(string input, Config config, CancellationToken ct = default) =>
        HandleWeeklyCampaignAsync(input, config, "Weekly Grands",
            p => _api.GetWeeklyGrandCampaignsAsync(p, ct),
            id => _api.GetWeeklyGrandCampaignAsync(id, ct),
            c => _parser.ParseWeeklyGrandsNum(c.Name),
            weekId => Path.Combine(_defaultMapsFolder, "Weekly Grands"),
            (m, i, weekId) => $"{weekId} - ", ct);

    private async Task<List<string>> HandleWeeklyCampaignAsync(
        string input, Config config, string displayName,
        Func<int, Task<ICampaignCollection>> fetchAllFunc,
        Func<int, Task<ICampaign>> fetchOneFunc,
        Func<ICampaignItem, int> idParserFunc,
        Func<string, string> downloadDirFunc,
        Func<IMap, int, string, string> prefixFunc,
        CancellationToken ct)
    {
        List<string> downloadedPaths = [];
        _console.WriteLine($"Fetching available {displayName} campaigns...");
        var allCampaigns = await FetchAllCampaignsAsync(fetchAllFunc, ct: ct);

        var campaignWithNums = allCampaigns
            .Select(c => new { Campaign = c, Num = idParserFunc(c) })
            .Where(x => x.Num != -1)
            .OrderBy(x => x.Num)
            .ToList();

        if (!campaignWithNums.Any()) return downloadedPaths;

        List<(MapRef Start, MapRef End)> ranges = [];
        if (input.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            var latestNum = campaignWithNums.Max(x => x.Num);
            ranges.Add((new MapRef(latestNum), new MapRef(latestNum)));
        }
        else if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var minNum = campaignWithNums.Min(x => x.Num);
            var maxNum = campaignWithNums.Max(x => x.Num);
            ranges.Add((new MapRef(minNum), new MapRef(maxNum)));
        }
        else
        {
            ranges = _parser.ParseMapRanges(input);
        }

        if (!ranges.Any()) return downloadedPaths;

        var campaignsByNum = campaignWithNums.GroupBy(x => x.Num).ToDictionary(g => g.Key, g => g.ToList());
        var relevantNums = campaignsByNum.Keys.Where(n => ranges.Any(r => n >= r.Start.Campaign && n <= r.End.Campaign)).OrderBy(n => n).ToList();

        foreach (var num in relevantNums)
        {
            var matches = campaignsByNum[num];
            ICampaignItem? campaignItem = null;

            if (matches.Count == 1 || !config.App.Interactive)
            {
                campaignItem = matches[0].Campaign;
            }
            else
            {
                var choice = await _console.SelectItemAsync($"Multiple campaigns found for {displayName} {num}", matches.Select(m => $"{TextFormatter.Deformat(m.Campaign.Name)} (ID: {m.Campaign.Id})"));
                if (choice > 0)
                {
                    campaignItem = matches[choice - 1].Campaign;
                }
                else continue;
            }

            _console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

            var fullCampaign = await fetchOneFunc(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var playlist = fullCampaign.Playlist.ToList();
            List<(IMap map, int index)> mapsToDownload = [];

            for (int i = 0; i < playlist.Count; i++)
            {
                int mapNum = i + 1;
                if (ranges.Any(r => IsInMapRange(num, mapNum, r.Start, r.End)))
                {
                    mapsToDownload.Add((playlist[i], i));
                }
            }

            if (!mapsToDownload.Any()) continue;

            var weekIdStr = num.ToString();
            var downloadDir = downloadDirFunc(weekIdStr);
            downloadedPaths.AddRange(await _downloader.DownloadAndFixMapsAsync(
                mapsToDownload.Select(m => new MapDownloadRecord(m.map.Name, m.map.FileName, m.map.FileUrl, prefixFunc(m.map, m.index, weekIdStr))),
                downloadDir, config, ct));
        }
        return downloadedPaths;
    }

    private static bool IsInMapRange(int campaignNum, int mapNum, MapRef start, MapRef end)
    {
        if (campaignNum < start.Campaign || campaignNum > end.Campaign) return false;
        if (campaignNum == start.Campaign && start.Map.HasValue && mapNum < start.Map.Value) return false;
        if (campaignNum == end.Campaign && end.Map.HasValue && mapNum > end.Map.Value) return false;
        return true;
    }

    public async Task<List<string>> HandleSeasonalAsync(string input, Config config, CancellationToken ct = default)
    {
        List<string> downloadedPaths = [];
        _console.WriteLine("Fetching available Seasonal campaigns...");
        var allCampaigns = await FetchAllCampaignsAsync(p => _api.GetSeasonalCampaignsAsync(p, ct), ct: ct);

        var campaignRefs = allCampaigns
            .Select(c => new { Campaign = c, Ref = _parser.ParseSeasonalRefFromCampaignName(c.Name) })
            .Where(x => x.Ref != null)
            .OrderBy(x => x.Ref)
            .ToList();

        List<(SeasonalRef Start, SeasonalRef End)> ranges = [];
        if (input.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            if (campaignRefs.Any())
            {
                var latest = campaignRefs.Last().Ref!;
                ranges.Add((latest, latest));
            }
        }
        else if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (campaignRefs.Any())
            {
                ranges.Add((campaignRefs.First().Ref!, campaignRefs.Last().Ref!));
            }
        }
        else
        {
            ranges = _parser.ParseSeasonalRanges(input);
        }

        if (!ranges.Any())
        {
            // Fallback to name search
            var matches = allCampaigns.Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!matches.Any())
            {
                _console.WriteLine($"Error: Could not find seasonal campaign matching '{input}'.");
                return downloadedPaths;
            }

            ICampaignItem? campaignItem = null;
            if (matches.Count == 1 || !config.App.Interactive)
            {
                campaignItem = matches[0];
            }
            else
            {
                var choice = await _console.SelectItemAsync("Multiple seasonal campaigns found", matches.Select(m => $"{TextFormatter.Deformat(m.Name)} (ID: {m.Id})"));
                if (choice > 0)
                {
                    campaignItem = matches[choice - 1];
                }
                else return downloadedPaths;
            }

            _console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");
            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id, ct);
            if (fullCampaign?.Playlist == null) return downloadedPaths;

            var seasonalFolderName = PathUtilities.SanitizeFolderName(_parser.FormatSeasonalFolderName(campaignItem.Name));
            var downloadDir = Path.Combine(_defaultMapsFolder, "Seasonal", seasonalFolderName);
            return await _downloader.DownloadAndFixMapsAsync(fullCampaign.Playlist.Select((m, i) => new MapDownloadRecord(m.Name, m.FileName, m.FileUrl, $"{(i + 1):D2} - ")), downloadDir, config, ct);
        }

        foreach (var campaignRef in campaignRefs)
        {
            var currentRef = campaignRef.Ref!;
            bool campaignInRange = ranges.Any(r => IsInSeasonalRange(currentRef, r.Start, r.End));
            if (!campaignInRange) continue;

            var campaignItem = campaignRef.Campaign;
            _console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id, ct);
            if (fullCampaign?.Playlist == null) continue;

            var playlist = fullCampaign.Playlist.ToList();
            List<(IMap map, int index)> mapsToDownload = [];

            for (int i = 0; i < playlist.Count; i++)
            {
                int mapNum = i + 1;
                if (ranges.Any(r => IsInSeasonalMapRange(currentRef, mapNum, r.Start, r.End)))
                {
                    mapsToDownload.Add((playlist[i], i));
                }
            }

            if (!mapsToDownload.Any()) continue;

            var seasonalFolderName = PathUtilities.SanitizeFolderName(_parser.FormatSeasonalFolderName(campaignItem.Name));
            var downloadDir = Path.Combine(_defaultMapsFolder, "Seasonal", seasonalFolderName);
            downloadedPaths.AddRange(await _downloader.DownloadAndFixMapsAsync(
                mapsToDownload.Select(m => new MapDownloadRecord(m.map.Name, m.map.FileName, m.map.FileUrl, $"{(m.index + 1):D2} - ")),
                downloadDir, config, ct));
        }

        return downloadedPaths;
    }

    private static bool IsInSeasonalRange(SeasonalRef current, SeasonalRef start, SeasonalRef end)
    {
        var c = new SeasonalRef(current.Year, current.SeasonOrder);
        var s = new SeasonalRef(start.Year, start.SeasonOrder);
        var e = new SeasonalRef(end.Year, end.SeasonOrder);
        return c.CompareTo(s) >= 0 && c.CompareTo(e) <= 0;
    }

    private static bool IsInSeasonalMapRange(SeasonalRef currentCampaign, int mapNum, SeasonalRef start, SeasonalRef end)
    {
        if (!IsInSeasonalRange(currentCampaign, start, end)) return false;

        if (currentCampaign.Year == start.Year && currentCampaign.SeasonOrder == start.SeasonOrder)
        {
            if (start.Map.HasValue && mapNum < start.Map.Value) return false;
        }

        if (currentCampaign.Year == end.Year && currentCampaign.SeasonOrder == end.SeasonOrder)
        {
            if (end.Map.HasValue && mapNum > end.Map.Value) return false;
        }

        return true;
    }


    public async Task<List<string>> HandleClubCampaignAsync(string input, Config config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _console.WriteLine("Error: Club campaign input is empty.");
            return [];
        }

        int clubId, campaignId;
        var parts = input.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out clubId) && int.TryParse(parts[1], out campaignId))
        {
            return await DownloadClubCampaignAsync(clubId, campaignId, config, ct);
        }
        else if (int.TryParse(input, out clubId))
        {
            _console.WriteLine($"Fetching all campaigns for Club ID {clubId}...");
            var allCampaigns = await FetchAllCampaignsAsync(p => _api.GetClubCampaignsAsync(p, ct), ct: ct);
            var clubCampaigns = allCampaigns.Where(c => c.ClubId == clubId).ToList();
            if (!clubCampaigns.Any())
            {
                _console.WriteLine($"No campaigns found for Club ID {clubId} (searched all pages).");
                return [];
            }

            List<string> downloadedPaths = [];
            foreach (var campaign in clubCampaigns)
            {
                _console.WriteLine($"Found: {TextFormatter.Deformat(campaign.Name)} (ID: {campaign.Id})");
                downloadedPaths.AddRange(await DownloadClubCampaignAsync(clubId, campaign.Id, config, ct));
            }
            return downloadedPaths;
        }
        else
        {
            _console.WriteLine($"Searching for club campaigns matching '{input}'...");
            var campaigns = await FetchAllCampaignsAsync(p => _api.GetClubCampaignsAsync(p, ct), maxPages: 20, ct: ct);
            var matches = campaigns.Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0)
            {
                _console.WriteLine($"Error: No matching club campaigns found for '{input}'.");
            }
            else if (matches.Count == 1 || !config.App.Interactive)
            {
                var match = matches[0];
                _console.WriteLine($"Found: {TextFormatter.Deformat(match.Name)} (ID: {match.ClubId}/{match.Id})");
                return await DownloadClubCampaignAsync(match.ClubId ?? 0, match.Id, config, ct);
            }
            else
            {
                var choice = await _console.SelectItemAsync("Multiple campaigns found", matches.Select(m => $"{TextFormatter.Deformat(m.Name)} (ID: {m.ClubId}/{m.Id})"));
                if (choice > 0)
                {
                    var match = matches[choice - 1];
                    return await DownloadClubCampaignAsync(match.ClubId ?? 0, match.Id, config, ct);
                }
            }
        }
        return [];
    }

    private async Task<List<string>> DownloadClubCampaignAsync(int clubId, int campaignId, Config config, CancellationToken ct)
    {
        var fullCampaign = await _api.GetClubCampaignAsync(clubId, campaignId, ct);
        if (fullCampaign?.Playlist == null) return [];

        var clubPart = PathUtilities.SanitizeFolderName(!string.IsNullOrEmpty(fullCampaign.ClubName) ? fullCampaign.ClubName : clubId.ToString());
        var campaignPart = PathUtilities.SanitizeFolderName(!string.IsNullOrEmpty(fullCampaign.Name) ? fullCampaign.Name : campaignId.ToString());

        var downloadDir = Path.Combine(_defaultMapsFolder, "Clubs", clubPart, campaignPart);
        return await _downloader.DownloadAndFixMapsAsync(fullCampaign.Playlist.Select((m, i) => new MapDownloadRecord(m.Name, m.FileName, m.FileUrl, $"{(i + 1):D2} - ")), downloadDir, config, ct);
    }

    public async Task<List<string>> HandleTmxMapsAsync(string input, Config config, CancellationToken ct = default)
    {
        var ids = _parser.ParseTmxIds(input);
        if (!ids.Any()) return [];

        List<ITmxMap> maps = [];
        foreach (var id in ids)
        {
            var map = await _api.GetTmxMapAsync(id, ct);
            if (map != null) maps.Add(map);
            else _console.WriteLine($"Error: Could not find TMX map {id}.");
        }

        return await HandleTmxMapsAsync(maps, config, ct);
    }

    public async Task<List<string>> HandleTmxMapsAsync(IEnumerable<ITmxMap> maps, Config config, CancellationToken ct = default)
    {
        var downloadDir = Path.Combine(_defaultMapsFolder, "Exchange");
        var mapsToDownload = maps.Select(map => new MapDownloadRecord(map.Name, null, _api.GetTmxMapUrl(map.Id), null));
        return await _downloader.DownloadAndFixMapsAsync(mapsToDownload, downloadDir, config, ct);
    }

    public async Task<List<string>> HandleTmxPacksAsync(string input, Config config, CancellationToken ct = default)
    {
        List<string> downloadedPaths = [];
        var ids = _parser.ParseTmxIds(input);
        if (!ids.Any()) return downloadedPaths;

        foreach (var id in ids)
        {
            var pack = await _api.GetTmxMapPackAsync(id, ct);
            if (pack == null)
            {
                _console.WriteLine($"Error: Could not find TMX map pack {id}.");
                continue;
            }

            var deformattedPackName = TextFormatter.Deformat(pack.Name);
            _console.WriteLine($"Found Map Pack: {deformattedPackName}");
            var maps = await _api.GetTmxMapPackMapsAsync(pack.Id, ct);
            var downloadDir = Path.Combine(_defaultMapsFolder, "Exchange", PathUtilities.SanitizeFolderName(deformattedPackName));

            downloadedPaths.AddRange(await _downloader.DownloadAndFixMapsAsync(maps.Select((m, i) => new MapDownloadRecord(m.Name, null, _api.GetTmxMapUrl(m.Id), $"{(i + 1):D2} - ")), downloadDir, config, ct));
        }

        return downloadedPaths;
    }

    public async Task<List<string>> HandleTmxSearchAsync(string? name, string? author, string sort, bool desc, Config config, CancellationToken ct = default)
    {
        _console.WriteLine($"Searching TMX (Name: {name ?? "Any"}, Author: {author ?? "Any"}, Sort: {sort}, Desc: {desc})...");
        var results = (await _api.SearchTmxMapsAsync(name, author, sort, desc, ct)).ToList();

        if (results.Count == 0)
        {
            _console.WriteLine("No TMX maps found.");
            return [];
        }

        if (!config.App.Interactive)
        {
            var match = results[0];
            _console.WriteLine($"Found: {TextFormatter.Deformat(match.Name)} by {match.AuthorName} (ID: {match.Id})");
            return await HandleTmxMapsAsync([match], config, ct);
        }

        var displayResults = results.Take(20).ToList();
        var choice = await _console.SelectItemAsync("Search results", displayResults.Select(r => $"{TextFormatter.Deformat(r.Name)} by {r.AuthorName} (ID: {r.Id}, Awards: {r.AwardCount}, DLs: {r.DownloadCount})"));
        if (choice > 0)
        {
            return await HandleTmxMapsAsync([displayResults[choice - 1]], config, ct);
        }

        return [];
    }

    public async Task<List<string>> HandleTmxRandomAsync(Config config, CancellationToken ct = default)
    {
        _console.WriteLine("Fetching random map from TMX...");
        var map = await _api.GetRandomTmxMapAsync(ct);
        if (map == null)
        {
            _console.WriteLine("Error: Failed to fetch random map.");
            return [];
        }

        _console.WriteLine($"Random Map: {TextFormatter.Deformat(map.Name)} by {map.AuthorName} (ID: {map.Id})");
        return await HandleTmxMapsAsync([map], config, ct);
    }


    public async Task<List<string>> HandleTrackOfTheDayAsync(string dateInput, Config config, CancellationToken ct = default)
    {
        var now = _dateTime.UtcNow;
        List<(DateTime Start, DateTime End)> ranges = [];

        if (dateInput.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            var latestDate = now.Date;
            if (now.Hour < TotdReleaseHour) latestDate = latestDate.AddDays(-1);
            ranges.Add((latestDate, latestDate));
        }
        else if (dateInput.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            ranges.Add((new DateTime(2020, 7, 1), now.Date));
        }
        else
        {
            ranges = _parser.ParseToTdRanges(dateInput, now);
        }

        if (!ranges.Any()) return [];

        // Check for API-to-System month drift
        int monthDrift = 0;
        try
        {
            var currentMonthFromApi = await _api.GetTrackOfTheDaysAsync(0, ct);
            if (currentMonthFromApi != null)
            {
                monthDrift = (now.Year - currentMonthFromApi.Year) * 12 + (now.Month - currentMonthFromApi.Month);
            }
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Warning: Could not determine API month drift: {ex.Message}");
        }

        List<string> downloadedPaths = [];
        HashSet<DateTime> allDaysToDownload = [];
        foreach (var range in ranges)
        {
            for (var d = range.Start; d <= range.End; d = d.AddDays(1))
            {
                allDaysToDownload.Add(d);
            }
        }

        var daysByMonth = allDaysToDownload.GroupBy(d => new { d.Year, d.Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

        foreach (var monthGroup in daysByMonth)
        {
            int monthOffset = (now.Year - monthGroup.Key.Year) * 12 + (now.Month - monthGroup.Key.Month) - monthDrift;
            var response = await _api.GetTrackOfTheDaysAsync(monthOffset, ct);
            if (response?.Days == null) continue;

            var targetDays = monthGroup.Select(d => d.Day).ToList();
            var totdDays = response.Days.Where(d => d.Map != null && targetDays.Contains(d.MonthDay)).OrderBy(d => d.MonthDay).ToList();

            if (!totdDays.Any()) continue;

            var downloadDir = Path.Combine(_defaultMapsFolder, "Track of the Day", response.Year.ToString(), response.Month.ToString("D2"));
            downloadedPaths.AddRange(await _downloader.DownloadAndFixMapsAsync(totdDays.Select(d => new MapDownloadRecord(d.Map!.Name, d.Map.FileName, d.Map.FileUrl, $"{d.MonthDay:D2} - ")), downloadDir, config, ct));
        }

        return downloadedPaths;
    }

    private async Task<List<ICampaignItem>> FetchAllCampaignsAsync(Func<int, Task<ICampaignCollection>> fetchFunc, int maxPages = int.MaxValue, CancellationToken ct = default)
    {
        List<ICampaignItem> all = [];
        int page = 0, pageCount = 1;
        while (page < pageCount && page < maxPages)
        {
            var response = await fetchFunc(page);
            if (response?.Campaigns != null) all.AddRange(response.Campaigns);
            pageCount = response?.PageCount ?? 0;
            page++;
            if (ct.IsCancellationRequested) break;
        }
        return all;
    }

    public async Task HandleExportCampaignMedalsAsync(string playerId, string? campaignNameFilter, Config config, string? outputPath = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(playerId, out var accountId))
        {
            _console.WriteLine("Error: Player ID must be a valid GUID.");
            return;
        }

        string accountIdStr = accountId.ToString();
        _console.WriteLine($"Exporting medals for Player ID: {accountIdStr}");

        _console.WriteLine("Fetching available Seasonal campaigns...");
        var allCampaigns = await FetchAllCampaignsAsync(p => _api.GetSeasonalCampaignsAsync(p, ct), ct: ct);

        var campaignsToProcess = allCampaigns;
        if (!string.IsNullOrEmpty(campaignNameFilter))
        {
            campaignsToProcess = allCampaigns.Where(c => c.Name.Contains(campaignNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!campaignsToProcess.Any())
            {
                _console.WriteLine($"Error: No seasonal campaigns found matching '{campaignNameFilter}'.");
                return;
            }
        }

        campaignsToProcess = [.. campaignsToProcess.OrderBy(c => c.Id)];

        List<string> csvLines = ["Campaign Name,Track Name,Medal,Best Time"];

        for (int i = 0; i < campaignsToProcess.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var campaignItem = campaignsToProcess[i];
            var deformattedCampaignName = TextFormatter.Deformat(campaignItem.Name);
            _console.WriteLine($"[{i + 1}/{campaignsToProcess.Count}] Campaign: {deformattedCampaignName}");

            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id, ct);
            if (fullCampaign?.Playlist == null)
            {
                _console.WriteLine("  Failed to fetch campaign details.");
                continue;
            }

            foreach (var map in fullCampaign.Playlist)
            {
                ct.ThrowIfCancellationRequested();
                var deformattedMapName = TextFormatter.Deformat(map.Name);
                _console.Write($"  - {deformattedMapName}... ");

                int medal = 0;
                string formattedTime = "00:00:00:000";

                try
                {
                    var leaderboard = await _api.GetLeaderboardAsync(map.MapUid, accountIdStr, ct);
                    if (leaderboard.Tops != null && leaderboard.Tops.Any())
                    {
                        var record = leaderboard.Tops.First();
                        var t = record.Time;

                        if (t.TotalMilliseconds <= map.AuthorScore.TotalMilliseconds) medal = 4;
                        else if (t.TotalMilliseconds <= map.GoldScore.TotalMilliseconds) medal = 3;
                        else if (t.TotalMilliseconds <= map.SilverScore.TotalMilliseconds) medal = 2;
                        else if (t.TotalMilliseconds <= map.BronzeScore.TotalMilliseconds) medal = 1;

                        formattedTime = $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds:D3}";
                        _console.WriteLine($"Time: {formattedTime}, Medal: {medal}");
                    }
                    else
                    {
                        _console.WriteLine("No record.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _console.WriteLine("No record found (API returned 404/500).");
                    }
                    else
                    {
                        _console.WriteLine($"Network error: {ex.StatusCode}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _console.WriteLine($"Error: {ex.Message}");
                }

                csvLines.Add($"{CsvUtilities.EscapeCsv(deformattedCampaignName)},{CsvUtilities.EscapeCsv(deformattedMapName)},{medal},{formattedTime}");
            }
        }

        var finalPath = outputPath ?? Path.Combine(_defaultMapsFolder, "medals.csv");
        try
        {
            var dir = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(dir) && !_fs.DirectoryExists(dir)) _fs.CreateDirectory(dir);

            await _fs.WriteAllLinesAsync(finalPath, csvLines, ct);
            _console.WriteLine($"\nExport complete! Saved to {Path.GetFullPath(finalPath)}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _console.WriteLine($"\nError saving CSV: {ex.Message}");
        }
    }

    public async Task<List<string>> RunBatchFixerAsync(Config config, List<string>? extraFiles = null, CancellationToken ct = default)
    {
        ConcurrentBag<string> processedPaths = [];
        HashSet<string> filesToProcess = [];

        if (extraFiles != null)
        {
            foreach (var path in extraFiles)
            {
                if (_fs.DirectoryExists(path))
                {
                    foreach (var file in _fs.GetFiles(path, "*.Map.Gbx", SearchOption.AllDirectories))
                        filesToProcess.Add(file);
                }
                else if (_fs.FileExists(path))
                {
                    filesToProcess.Add(path);
                }
            }
        }

        if (config.Fixer.ExplicitFolder || (extraFiles == null || extraFiles.Count == 0))
        {
            if (_fs.DirectoryExists(config.Fixer.FolderPath))
            {
                foreach (var file in _fs.GetFiles(config.Fixer.FolderPath, "*.Map.Gbx", SearchOption.AllDirectories))
                    filesToProcess.Add(file);
            }
        }

        if (filesToProcess.Count == 0) return [];

        _console.WriteLine($"\nAnalyzing {filesToProcess.Count} maps...");
        int changed = 0;

        await Parallel.ForEachAsync(filesToProcess, ct, async (file, token) =>
        {
            try
            {
                if (await _fixer.ProcessFileAsync(file, config, token))
                {
                    Interlocked.Increment(ref changed);
                }
                processedPaths.Add(file);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _console.WriteLine($"Failed to process {file}: {ex.Message}");
            }
        });

        if (config.Fixer.UpdateTitle || config.Fixer.ConvertPlatformMapType)
            _console.WriteLine($"\nBatch analysis complete. Analyzed: {filesToProcess.Count}, Updated: {changed}");

        return [.. processedPaths];
    }
}
