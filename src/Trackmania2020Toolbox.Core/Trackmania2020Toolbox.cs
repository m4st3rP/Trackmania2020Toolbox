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
using ManiaAPI.TMX;
using TmEssentials;

[assembly: InternalsVisibleTo("Trackmania2020Toolbox.Tests")]

namespace Trackmania2020Toolbox;

public interface ICampaignItem { int Id { get; } string Name { get; } int? ClubId { get; } }
public interface ICampaign { IEnumerable<IMap> Playlist { get; } string Name { get; } string? ClubName { get; } }
public interface IMap { string Name { get; } string? FileName { get; } string? FileUrl { get; } string MapUid { get; } TimeInt32 AuthorScore { get; } TimeInt32 GoldScore { get; } TimeInt32 SilverScore { get; } TimeInt32 BronzeScore { get; } }
public interface ICampaignCollection { IEnumerable<ICampaignItem> Campaigns { get; } int PageCount { get; } }
public interface ITrackOfTheDayDay { int MonthDay { get; } ITrackmaniaMap? Map { get; } }
public interface ITrackmaniaMap { string Name { get; } string? FileName { get; } string? FileUrl { get; } }
public interface ITrackOfTheDayCollection { int Year { get; } int Month { get; } IEnumerable<ITrackOfTheDayDay> Days { get; } }
public interface ILeaderboard { IEnumerable<IRecord> Tops { get; } }
public interface IRecord { TimeInt32 Time { get; } }

public interface ITmxMap { int Id { get; } string Name { get; } string AuthorName { get; } int AwardCount { get; } int DownloadCount { get; } }
public interface ITmxMapPack { int Id { get; } string Name { get; } }

public interface ITrackmaniaApi : IDisposable
{
    Task<ICampaignCollection> GetWeeklyShortCampaignsAsync(int page);
    Task<ICampaign> GetWeeklyShortCampaignAsync(int id);
    Task<ICampaignCollection> GetWeeklyGrandCampaignsAsync(int page);
    Task<ICampaign> GetWeeklyGrandCampaignAsync(int id);
    Task<ICampaignCollection> GetSeasonalCampaignsAsync(int page);
    Task<ICampaign> GetSeasonalCampaignAsync(int id);
    Task<ICampaignCollection> GetClubCampaignsAsync(int page);
    Task<ICampaign> GetClubCampaignAsync(int clubId, int campaignId);
    Task<ITrackOfTheDayCollection> GetTrackOfTheDaysAsync(int monthOffset);
    Task<ILeaderboard> GetLeaderboardAsync(string mapUid, string accountId);

    Task<ITmxMap?> GetTmxMapAsync(int id);
    string GetTmxMapUrl(int id);
    Task<IEnumerable<ITmxMap>> SearchTmxMapsAsync(string? name, string? author, string sort, bool desc);
    Task<ITmxMap?> GetRandomTmxMapAsync();
    Task<ITmxMapPack?> GetTmxMapPackAsync(int id);
    Task<IEnumerable<ITmxMap>> GetTmxMapPackMapsAsync(int id);
}

public interface IFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    bool FileExists(string path);
    Task WriteAllBytesAsync(string path, byte[] bytes);
    void WriteAllText(string path, string contents);
    string[] ReadAllLines(string path);
    void WriteAllLines(string path, IEnumerable<string> contents);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
}

public interface INetworkService
{
    Task<byte[]> GetByteArrayAsync(string url);
}

public interface IMapFixer
{
    bool ProcessFile(string filePath, Config config);
}

public interface IDateTime { DateTime UtcNow { get; } }

public interface IConsole
{
    void WriteLine(string? value = null);
    void Write(string? value = null);
    string? ReadLine();
    Task<int> SelectItemAsync(string title, IEnumerable<string> items);
}

public record Config(
    string? WeeklyShorts, string? WeeklyGrands, string? Seasonal, string? ClubCampaign, string? ToTDDate,
    string? TmxMaps, string? TmxPacks, string? TmxSearch, string? TmxAuthor, string TmxSort, bool TmxDesc, bool TmxRandom,
    string? ExportMedalsPlayerId, string? ExportMedalsCampaign,
    string FolderPath, bool ExplicitFolder, bool UpdateTitle, bool ConvertPlatformMapType, bool DryRun,
    bool ForceOverwrite, bool Interactive, bool Play, string? SetGamePath, List<string> ExtraPaths
);

public class TrackmaniaApiWrapper : ITrackmaniaApi
{
    private readonly TrackmaniaIO _api;
    private readonly MX _tmx;

    public TrackmaniaApiWrapper(HttpClient httpClient, string userAgent)
    {
        _api = new TrackmaniaIO(userAgent);
        _tmx = new MX(httpClient, TmxSite.Trackmania);
    }

    public async Task<ICampaignCollection> GetWeeklyShortCampaignsAsync(int page) => new CampaignCollectionProxy(await _api.GetWeeklyShortCampaignsAsync(page));
    public async Task<ICampaign> GetWeeklyShortCampaignAsync(int id) => new CampaignProxy(await _api.GetWeeklyShortCampaignAsync(id));
    public async Task<ICampaignCollection> GetWeeklyGrandCampaignsAsync(int page) => new CampaignCollectionProxy(await _api.GetWeeklyGrandCampaignsAsync(page));
    public async Task<ICampaign> GetWeeklyGrandCampaignAsync(int id) => new CampaignProxy(await _api.GetWeeklyGrandCampaignAsync(id));
    public async Task<ICampaignCollection> GetSeasonalCampaignsAsync(int page) => new CampaignCollectionProxy(await _api.GetSeasonalCampaignsAsync(page));
    public async Task<ICampaign> GetSeasonalCampaignAsync(int id) => new CampaignProxy(await _api.GetSeasonalCampaignAsync(id));
    public async Task<ICampaignCollection> GetClubCampaignsAsync(int page) => new CampaignCollectionProxy(await _api.GetClubCampaignsAsync(page));
    public async Task<ICampaign> GetClubCampaignAsync(int clubId, int campaignId) => new CampaignProxy(await _api.GetClubCampaignAsync(clubId, campaignId));

    public async Task<ITrackOfTheDayCollection> GetTrackOfTheDaysAsync(int monthOffset)
    {
        var result = await _api.GetTrackOfTheDaysAsync(monthOffset);
        return new TrackOfTheDayCollectionProxy(result);
    }

    public async Task<ILeaderboard> GetLeaderboardAsync(string mapUid, string accountId)
    {
        var result = await _api.GetLeaderboardAsync(mapUid, accountId);
        return new LeaderboardProxy(result);
    }

    public async Task<ITmxMap?> GetTmxMapAsync(int id)
    {
        var result = await _tmx.SearchMapsAsync(new MX.SearchMapsParameters { Id = new long[] { id } });
        return result.Results.Count > 0 ? new TmxMapProxy(result.Results[0]) : null;
    }

    public string GetTmxMapUrl(int id) => _tmx.GetMapGbxUrl(id);

    public async Task<IEnumerable<ITmxMap>> SearchTmxMapsAsync(string? name, string? author, string sort, bool desc)
    {
        int? order = sort.ToLowerInvariant() switch
        {
            "name" => desc ? 2 : 1,
            "author" => desc ? 4 : 3,
            "awards" => desc ? 12 : 11,
            "downloads" => desc ? 20 : 19,
            _ => desc ? 2 : 1
        };

        var result = await _tmx.SearchMapsAsync(new MX.SearchMapsParameters { Name = name, Author = author, Order1 = order });
        return result.Results.Select(r => new TmxMapProxy(r));
    }

    public async Task<ITmxMap?> GetRandomTmxMapAsync()
    {
        var result = await _tmx.SearchMapsAsync(new MX.SearchMapsParameters { Random = 1, Count = 1 });
        return result.Results.Count > 0 ? new TmxMapProxy(result.Results[0]) : null;
    }

    public async Task<ITmxMapPack?> GetTmxMapPackAsync(int id)
    {
        var result = await _tmx.SearchMappacksAsync(new MX.SearchMappacksParameters { Id = new long[] { id } });
        return result.Results.Count > 0 ? new TmxMapPackProxy(result.Results[0]) : null;
    }

    public async Task<IEnumerable<ITmxMap>> GetTmxMapPackMapsAsync(int id)
    {
        var result = await _tmx.SearchMapsAsync(new MX.SearchMapsParameters { MappackId = id });
        return result.Results.Select(r => new TmxMapProxy(r));
    }

    public void Dispose()
    {
        _api.Dispose();
        _tmx.Dispose();
    }

    private class TmxMapProxy : ITmxMap
    {
        private readonly MapItem _obj;
        public TmxMapProxy(MapItem obj) => _obj = obj;
        public int Id => (int)_obj.MapId;
        public string Name => _obj.Name;
        public string AuthorName => _obj.Uploader.Name;
        public int AwardCount => _obj.AwardCount;
        public int DownloadCount => _obj.DownloadCount;
    }

    private class TmxMapPackProxy : ITmxMapPack
    {
        private readonly MappackItem _obj;
        public TmxMapPackProxy(MappackItem obj) => _obj = obj;
        public int Id => (int)_obj.MappackId;
        public string Name => _obj.Name;
    }

    private class CampaignCollectionProxy : ICampaignCollection
    {
        private readonly CampaignCollection _obj;
        public CampaignCollectionProxy(CampaignCollection obj) => _obj = obj;
        public IEnumerable<ICampaignItem> Campaigns => _obj.Campaigns.Select(c => new CampaignItemProxy(c));
        public int PageCount => _obj.PageCount;
    }

    private class CampaignItemProxy : ICampaignItem
    {
        private readonly CampaignItem _obj;
        public CampaignItemProxy(CampaignItem obj) => _obj = obj;
        public int Id => _obj.Id;
        public string Name => _obj.Name;
        public int? ClubId => _obj.ClubId;
    }

    private class CampaignProxy : ICampaign
    {
        private readonly Campaign _obj;
        public CampaignProxy(Campaign obj) => _obj = obj;
        public IEnumerable<IMap> Playlist => _obj.Playlist.Select(m => new MapProxy(m));
        public string Name => _obj.Name;
        public string? ClubName => _obj.ClubName;
    }

    private class MapProxy : IMap
    {
        private readonly Map _obj;
        public MapProxy(Map obj) => _obj = obj;
        public string Name => _obj.Name;
        public string? FileName => _obj.FileName;
        public string? FileUrl => _obj.FileUrl;
        public string MapUid => _obj.MapUid;
        public TimeInt32 AuthorScore => _obj.AuthorScore;
        public TimeInt32 GoldScore => _obj.GoldScore;
        public TimeInt32 SilverScore => _obj.SilverScore;
        public TimeInt32 BronzeScore => _obj.BronzeScore;
    }

    private class TrackOfTheDayCollectionProxy : ITrackOfTheDayCollection
    {
        private readonly TrackOfTheDayMonth _obj;
        public TrackOfTheDayCollectionProxy(TrackOfTheDayMonth obj) => _obj = obj;
        public int Year => _obj.Year;
        public int Month => _obj.Month;
        public IEnumerable<ITrackOfTheDayDay> Days => _obj.Days.Select(d => new TrackOfTheDayDayProxy(d));
    }

    private class TrackOfTheDayDayProxy : ITrackOfTheDayDay
    {
        private readonly TrackOfTheDay _obj;
        public TrackOfTheDayDayProxy(TrackOfTheDay obj) => _obj = obj;
        public int MonthDay => _obj.MonthDay;
        public ITrackmaniaMap? Map => _obj.Map != null ? new TrackmaniaMapProxy(_obj.Map) : null;
    }

    private class TrackmaniaMapProxy : ITrackmaniaMap
    {
        private readonly Map _obj;
        public TrackmaniaMapProxy(Map obj) => _obj = obj;
        public string Name => _obj.Name;
        public string? FileName => _obj.FileName;
        public string? FileUrl => _obj.FileUrl;
    }

    private class LeaderboardProxy : ILeaderboard
    {
        private readonly Leaderboard _obj;
        public LeaderboardProxy(Leaderboard obj) => _obj = obj;
        public IEnumerable<IRecord> Tops => _obj.Tops.Select(r => new RecordProxy(r));
    }

    private class RecordProxy : IRecord
    {
        private readonly Record _obj;
        public RecordProxy(Record obj) => _obj = obj;
        public TimeInt32 Time => _obj.Time;
    }
}

public class RealFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool FileExists(string path) => File.Exists(path);
    public Task WriteAllBytesAsync(string path, byte[] bytes) => File.WriteAllBytesAsync(path, bytes);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public string[] ReadAllLines(string path) => File.ReadAllLines(path);
    public void WriteAllLines(string path, IEnumerable<string> contents) => File.WriteAllLines(path, contents);
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);
}

public class RealNetworkService : INetworkService
{
    private readonly HttpClient _httpClient;
    public RealNetworkService(HttpClient httpClient) => _httpClient = httpClient;
    public Task<byte[]> GetByteArrayAsync(string url) => _httpClient.GetByteArrayAsync(url);
}

public class RealMapFixer : IMapFixer
{
    public bool ProcessFile(string filePath, Config cfg)
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
            if (!cfg.DryRun)
            {
                gbx.Save(filePath);
            }
        }

        return changed;
    }
}

public class RealDateTime : IDateTime { public DateTime UtcNow => DateTime.UtcNow; }


public class ToolboxApp
{
    private const int TotdReleaseHour = 17;
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars()
        .Union(new[] { '/', '\\', ':', '*', '?', '\"', '<', '>', '|' })
        .Distinct()
        .ToArray();

    private readonly ITrackmaniaApi _api;
    private readonly IFileSystem _fs;
    private readonly INetworkService _net;
    private readonly IMapFixer _fixer;
    private readonly IConsole _console;
    private readonly IDateTime _dateTime;
    public readonly string _scriptDirectory;
    public readonly string _defaultMapsFolder;

    public ToolboxApp(ITrackmaniaApi api, IFileSystem fs, INetworkService net, IMapFixer fixer, IConsole console, IDateTime dateTime, string scriptDirectory)
    {
        _api = api;
        _fs = fs;
        _net = net;
        _fixer = fixer;
        _console = console;
        _dateTime = dateTime;
        _scriptDirectory = scriptDirectory;
        _defaultMapsFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Toolbox");
    }

    public async Task RunAsync(Config config)
    {
        if (config.SetGamePath != null)
        {
            SaveGamePath(config.SetGamePath);
            if (!config.Play && config.WeeklyShorts == null && config.WeeklyGrands == null &&
                config.Seasonal == null && config.ClubCampaign == null && config.ToTDDate == null &&
                config.ExportMedalsPlayerId == null && !config.ExplicitFolder && config.ExtraPaths.Count == 0)
                return;
        }

        var mapPaths = new List<string>();
        bool downloadActionTaken = false;

        if (config.WeeklyShorts != null)
        {
            mapPaths.AddRange(await HandleWeeklyShorts(config.WeeklyShorts, config));
            downloadActionTaken = true;
        }

        if (config.WeeklyGrands != null)
        {
            mapPaths.AddRange(await HandleWeeklyGrands(config.WeeklyGrands, config));
            downloadActionTaken = true;
        }

        if (config.Seasonal != null)
        {
            mapPaths.AddRange(await HandleSeasonal(config.Seasonal, config));
            downloadActionTaken = true;
        }

        if (config.ClubCampaign != null)
        {
            mapPaths.AddRange(await HandleClubCampaign(config.ClubCampaign, config));
            downloadActionTaken = true;
        }

        if (config.ToTDDate != null)
        {
            mapPaths.AddRange(await HandleTrackOfTheDay(config.ToTDDate, config));
            downloadActionTaken = true;
        }

        if (config.TmxMaps != null)
        {
            mapPaths.AddRange(await HandleTmxMaps(config.TmxMaps, config));
            downloadActionTaken = true;
        }

        if (config.TmxPacks != null)
        {
            mapPaths.AddRange(await HandleTmxPacks(config.TmxPacks, config));
            downloadActionTaken = true;
        }

        if (config.TmxSearch != null || config.TmxAuthor != null)
        {
            mapPaths.AddRange(await HandleTmxSearch(config.TmxSearch, config.TmxAuthor, config.TmxSort, config.TmxDesc, config));
            downloadActionTaken = true;
        }

        if (config.TmxRandom)
        {
            mapPaths.AddRange(await HandleTmxRandom(config));
            downloadActionTaken = true;
        }

        if (config.ExportMedalsPlayerId != null)
        {
            await HandleExportCampaignMedals(config.ExportMedalsPlayerId, config.ExportMedalsCampaign);
            downloadActionTaken = true;
        }

        if (config.ExplicitFolder || config.ExtraPaths.Count > 0 || (!downloadActionTaken && config.SetGamePath == null))
        {
            var fixedPaths = RunBatchFixer(config, config.ExtraPaths);
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

    private void SaveGamePath(string path)
    {
        var configPath = Path.Combine(_scriptDirectory, "config.toml");
        try
        {
            _fs.WriteAllText(configPath, $"game_path = \"{path}\"\n");
            _console.WriteLine($"Game path saved to: {configPath}");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    private string? GetGamePath()
    {
        var configPath = Path.Combine(_scriptDirectory, "config.toml");
        if (!_fs.FileExists(configPath)) return null;

        var lines = _fs.ReadAllLines(configPath);
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

    private void LaunchGame(List<string> mapPaths)
    {
        if (mapPaths.Count == 0)
        {
            _console.WriteLine("No maps to play.");
            return;
        }

        var gamePath = GetGamePath();
        if (string.IsNullOrEmpty(gamePath))
        {
            _console.WriteLine("Error: Trackmania.exe path not set.");
            _console.WriteLine("Please set it using: dotnet run --project src/Trackmania2020Toolbox.csproj -- --set-game-path \"C:\\Path\\To\\Trackmania.exe\"");
            return;
        }

        if (!_fs.FileExists(gamePath))
        {
            _console.WriteLine($"Error: Trackmania.exe not found at {gamePath}");
            return;
        }

        _console.WriteLine("\nNote: Trackmania needs to be already running for this to work correctly.");

        var sortedMaps = mapPaths.Distinct().OrderBy(p => p).ToList();
        var firstMap = sortedMaps.First();
        if (sortedMaps.Count > 1)
        {
            _console.WriteLine($"Note: Drag-and-drop only supports one map. Launching the first one: {Path.GetFileName(firstMap)}");
        }

        var arguments = $"\"{Path.GetFullPath(firstMap)}\"";

        try
        {
            _console.WriteLine($"Launching Trackmania with the map...");
            Process.Start(gamePath, arguments);
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error launching game: {ex.Message}");
        }
    }

    public Task<List<string>> HandleWeeklyShorts(string input, Config config) =>
        HandleWeeklyCampaign(input, config, "Weekly Shorts",
            p => _api.GetWeeklyShortCampaignsAsync(p),
            id => _api.GetWeeklyShortCampaignAsync(id),
            @"\bWeek 0*(\d+)\b",
            weekId => Path.Combine(_defaultMapsFolder, "Weekly Shorts", weekId),
            (m, i, weekId) => $"{(i + 1):D2} - ");

    public Task<List<string>> HandleWeeklyGrands(string input, Config config) =>
        HandleWeeklyCampaign(input, config, "Weekly Grands",
            p => _api.GetWeeklyGrandCampaignsAsync(p),
            id => _api.GetWeeklyGrandCampaignAsync(id),
            @"\bWeek Grand 0*(\d+)\b",
            weekId => Path.Combine(_defaultMapsFolder, "Weekly Grands"),
            (m, i, weekId) => $"{weekId} - ");

    private async Task<List<string>> HandleWeeklyCampaign(
        string input, Config config, string displayName,
        Func<int, Task<ICampaignCollection>> fetchAllFunc,
        Func<int, Task<ICampaign>> fetchOneFunc,
        string idRegexPattern,
        Func<string, string> downloadDirFunc,
        Func<IMap, int, string, string> prefixFunc)
    {
        var downloadedPaths = new List<string>();
        _console.WriteLine($"Fetching available {displayName} campaigns...");
        var allCampaigns = await FetchAllCampaigns(fetchAllFunc);

        var campaignWithNums = allCampaigns
            .Select(c => {
                var match = Regex.Match(c.Name, idRegexPattern, RegexOptions.IgnoreCase);
                return new { Campaign = c, Num = match.Success ? int.Parse(match.Groups[1].Value) : -1 };
            })
            .Where(x => x.Num != -1)
            .OrderBy(x => x.Num)
            .ToList();

        if (!campaignWithNums.Any()) return downloadedPaths;

        var ranges = new List<(MapRef Start, MapRef End)>();
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
            ranges = ParseMapRanges(input);
        }

        if (!ranges.Any()) return downloadedPaths;

        var campaignsByNum = campaignWithNums.GroupBy(x => x.Num).ToDictionary(g => g.Key, g => g.ToList());
        var relevantNums = campaignsByNum.Keys.Where(n => ranges.Any(r => n >= r.Start.Campaign && n <= r.End.Campaign)).OrderBy(n => n).ToList();

        foreach (var num in relevantNums)
        {
            var matches = campaignsByNum[num];
            ICampaignItem? campaignItem = null;

            if (matches.Count == 1 || !config.Interactive)
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
            var mapsToDownload = new List<(IMap map, int index)>();

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
            downloadedPaths.AddRange(await DownloadAndFixMaps(
                mapsToDownload.Select(m => (m.map.Name, (string?)m.map.FileName, (string?)m.map.FileUrl, (string?)prefixFunc(m.map, m.index, weekIdStr))),
                downloadDir, config));
        }
        return downloadedPaths;
    }

    private bool IsInMapRange(int campaignNum, int mapNum, MapRef start, MapRef end)
    {
        if (campaignNum < start.Campaign || campaignNum > end.Campaign) return false;
        if (campaignNum == start.Campaign && start.Map.HasValue && mapNum < start.Map.Value) return false;
        if (campaignNum == end.Campaign && end.Map.HasValue && mapNum > end.Map.Value) return false;
        return true;
    }

    public async Task<List<string>> HandleSeasonal(string input, Config config)
    {
        var downloadedPaths = new List<string>();
        _console.WriteLine("Fetching available Seasonal campaigns...");
        var allCampaigns = await FetchAllCampaigns(p => _api.GetSeasonalCampaignsAsync(p));

        var campaignRefs = allCampaigns
            .Select(c => new { Campaign = c, Ref = ParseSeasonalRefFromCampaignName(c.Name) })
            .Where(x => x.Ref != null)
            .OrderBy(x => x.Ref)
            .ToList();

        var ranges = new List<(SeasonalRef Start, SeasonalRef End)>();
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
            ranges = ParseSeasonalRanges(input);
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
            if (matches.Count == 1 || !config.Interactive)
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
            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) return downloadedPaths;

            var seasonalFolderName = FormatSeasonalFolderName(campaignItem.Name);
            var downloadDir = Path.Combine(_defaultMapsFolder, "Seasonal", seasonalFolderName);
            return await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
        }

        foreach (var campaignRef in campaignRefs)
        {
            var currentRef = campaignRef.Ref!;
            bool campaignInRange = ranges.Any(r => IsInSeasonalRange(currentRef, r.Start, r.End));
            if (!campaignInRange) continue;

            var campaignItem = campaignRef.Campaign;
            _console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var playlist = fullCampaign.Playlist.ToList();
            var mapsToDownload = new List<(IMap map, int index)>();

            for (int i = 0; i < playlist.Count; i++)
            {
                int mapNum = i + 1;
                if (ranges.Any(r => IsInSeasonalMapRange(currentRef, mapNum, r.Start, r.End)))
                {
                    mapsToDownload.Add((playlist[i], i));
                }
            }

            if (!mapsToDownload.Any()) continue;

            var seasonalFolderName = FormatSeasonalFolderName(campaignItem.Name);
            var downloadDir = Path.Combine(_defaultMapsFolder, "Seasonal", seasonalFolderName);
            downloadedPaths.AddRange(await DownloadAndFixMaps(
                mapsToDownload.Select(m => (m.map.Name, (string?)m.map.FileName, (string?)m.map.FileUrl, (string?)$"{(m.index + 1):D2} - ")),
                downloadDir, config));
        }

        return downloadedPaths;
    }

    private bool IsInSeasonalRange(SeasonalRef current, SeasonalRef start, SeasonalRef end)
    {
        var c = new SeasonalRef(current.Year, current.SeasonOrder);
        var s = new SeasonalRef(start.Year, start.SeasonOrder);
        var e = new SeasonalRef(end.Year, end.SeasonOrder);
        return c.CompareTo(s) >= 0 && c.CompareTo(e) <= 0;
    }

    private bool IsInSeasonalMapRange(SeasonalRef currentCampaign, int mapNum, SeasonalRef start, SeasonalRef end)
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

    public string FormatSeasonalFolderName(string campaignName)
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

    public async Task<List<string>> HandleClubCampaign(string input, Config config)
    {
        int clubId, campaignId;
        var parts = input.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out clubId) && int.TryParse(parts[1], out campaignId))
        {
            return await DownloadClubCampaign(clubId, campaignId, config);
        }
        else if (int.TryParse(input, out clubId))
        {
            _console.WriteLine($"Fetching all campaigns for Club ID {clubId}...");
            var allCampaigns = await FetchAllCampaigns(p => _api.GetClubCampaignsAsync(p));
            var clubCampaigns = allCampaigns.Where(c => c.ClubId == clubId).ToList();
            if (!clubCampaigns.Any())
            {
                _console.WriteLine($"No campaigns found for Club ID {clubId} (searched all pages).");
                return new List<string>();
            }

            var downloadedPaths = new List<string>();
            foreach (var campaign in clubCampaigns)
            {
                _console.WriteLine($"Found: {TextFormatter.Deformat(campaign.Name)} (ID: {campaign.Id})");
                downloadedPaths.AddRange(await DownloadClubCampaign(clubId, campaign.Id, config));
            }
            return downloadedPaths;
        }
        else
        {
            _console.WriteLine($"Searching for club campaigns matching '{input}'...");
            // Club campaigns can have thousands of pages, so we limit it to 20 pages for search
            // because there is no server-side search by name in the public Trackmania.io API.
            var campaigns = await FetchAllCampaigns(p => _api.GetClubCampaignsAsync(p), maxPages: 20);
            var matches = campaigns.Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0)
            {
                _console.WriteLine("No matching club campaigns found.");
            }
            else if (matches.Count == 1 || !config.Interactive)
            {
                var match = matches[0];
                _console.WriteLine($"Found: {TextFormatter.Deformat(match.Name)} (ID: {match.ClubId}/{match.Id})");
                return await DownloadClubCampaign(match.ClubId ?? 0, match.Id, config);
            }
            else
            {
                var choice = await _console.SelectItemAsync("Multiple campaigns found", matches.Select(m => $"{TextFormatter.Deformat(m.Name)} (ID: {m.ClubId}/{m.Id})"));
                if (choice > 0)
                {
                    var match = matches[choice - 1];
                    return await DownloadClubCampaign(match.ClubId ?? 0, match.Id, config);
                }
            }
        }
        return new List<string>();
    }

    private async Task<List<string>> DownloadClubCampaign(int clubId, int campaignId, Config config)
    {
        var fullCampaign = await _api.GetClubCampaignAsync(clubId, campaignId);
        if (fullCampaign?.Playlist == null) return new List<string>();

        var clubPart = !string.IsNullOrEmpty(fullCampaign.ClubName) ? fullCampaign.ClubName : clubId.ToString();
        var campaignPart = !string.IsNullOrEmpty(fullCampaign.Name) ? fullCampaign.Name : campaignId.ToString();

        var downloadDir = Path.Combine(_defaultMapsFolder, "Clubs", clubPart, campaignPart);
        return await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
    }

    public async Task<List<string>> HandleTmxMaps(string input, Config config)
    {
        var downloadedPaths = new List<string>();
        var ids = ParseTmxIds(input);
        if (!ids.Any()) return downloadedPaths;

        var downloadDir = Path.Combine(_defaultMapsFolder, "Exchange");
        var mapsToDownload = new List<(string Name, string? FileName, string? FileUrl, string? Prefix)>();

        foreach (var id in ids)
        {
            var map = await _api.GetTmxMapAsync(id);
            if (map != null)
            {
                mapsToDownload.Add((map.Name, null, _api.GetTmxMapUrl(map.Id), null));
            }
            else
            {
                _console.WriteLine($"Error: Could not find TMX map {id}.");
            }
        }

        return await DownloadAndFixMaps(mapsToDownload.Select(m => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)m.Prefix)), downloadDir, config);
    }

    public async Task<List<string>> HandleTmxPacks(string input, Config config)
    {
        var downloadedPaths = new List<string>();
        var ids = ParseTmxIds(input);
        if (!ids.Any()) return downloadedPaths;

        foreach (var id in ids)
        {
            var pack = await _api.GetTmxMapPackAsync(id);
            if (pack == null)
            {
                _console.WriteLine($"Error: Could not find TMX map pack {id}.");
                continue;
            }

            _console.WriteLine($"Found Map Pack: {TextFormatter.Deformat(pack.Name)}");
            var maps = await _api.GetTmxMapPackMapsAsync(pack.Id);
            var downloadDir = Path.Combine(_defaultMapsFolder, "Exchange", TextFormatter.Deformat(pack.Name));

            downloadedPaths.AddRange(await DownloadAndFixMaps(maps.Select((m, i) => (m.Name, (string?)null, (string?)_api.GetTmxMapUrl(m.Id), (string?)$"{(i + 1):D2} - ")), downloadDir, config));
        }

        return downloadedPaths;
    }

    public async Task<List<string>> HandleTmxSearch(string? name, string? author, string sort, bool desc, Config config)
    {
        _console.WriteLine($"Searching TMX (Name: {name ?? "Any"}, Author: {author ?? "Any"}, Sort: {sort}, Desc: {desc})...");
        var results = (await _api.SearchTmxMapsAsync(name, author, sort, desc)).ToList();

        if (results.Count == 0)
        {
            _console.WriteLine("No TMX maps found.");
            return new List<string>();
        }

        if (!config.Interactive)
        {
            var match = results[0];
            _console.WriteLine($"Found: {TextFormatter.Deformat(match.Name)} by {match.AuthorName} (ID: {match.Id})");
            return await HandleTmxMaps(match.Id.ToString(), config);
        }

        var displayResults = results.Take(20).ToList();
        var choice = await _console.SelectItemAsync("Search results", displayResults.Select(r => $"{TextFormatter.Deformat(r.Name)} by {r.AuthorName} (ID: {r.Id}, Awards: {r.AwardCount}, DLs: {r.DownloadCount})"));
        if (choice > 0)
        {
            return await HandleTmxMaps(displayResults[choice - 1].Id.ToString(), config);
        }

        return new List<string>();
    }

    public async Task<List<string>> HandleTmxRandom(Config config)
    {
        _console.WriteLine("Fetching random map from TMX...");
        var map = await _api.GetRandomTmxMapAsync();
        if (map == null)
        {
            _console.WriteLine("Error: Failed to fetch random map.");
            return new List<string>();
        }

        _console.WriteLine($"Random Map: {TextFormatter.Deformat(map.Name)} by {map.AuthorName} (ID: {map.Id})");
        return await HandleTmxMaps(map.Id.ToString(), config);
    }

    private List<int> ParseTmxIds(string input)
    {
        var result = new HashSet<int>();
        var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("https://trackmania.exchange/maps/", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(trimmed, @"/maps/(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id)) result.Add(id);
            }
            else if (trimmed.StartsWith("https://trackmania.exchange/mappack/", StringComparison.OrdinalIgnoreCase))
            {
                 var match = Regex.Match(trimmed, @"/mappack/(\d+)");
                 if (match.Success && int.TryParse(match.Groups[1].Value, out var id)) result.Add(id);
            }
            else if (int.TryParse(trimmed, out var num)) result.Add(num);
        }
        return result.ToList();
    }

    public async Task<List<string>> HandleTrackOfTheDay(string dateInput, Config config)
    {
        var now = _dateTime.UtcNow;
        var ranges = new List<(DateTime Start, DateTime End)>();

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
            ranges = ParseToTdRanges(dateInput, now);
        }

        if (!ranges.Any()) return new List<string>();

        var downloadedPaths = new List<string>();
        var allDaysToDownload = new List<DateTime>();
        foreach (var range in ranges)
        {
            for (var d = range.Start; d <= range.End; d = d.AddDays(1))
            {
                if (!allDaysToDownload.Contains(d)) allDaysToDownload.Add(d);
            }
        }

        var daysByMonth = allDaysToDownload.GroupBy(d => new { d.Year, d.Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

        foreach (var monthGroup in daysByMonth)
        {
            int monthOffset = (now.Year - monthGroup.Key.Year) * 12 + (now.Month - monthGroup.Key.Month);
            var response = await _api.GetTrackOfTheDaysAsync(monthOffset);
            if (response?.Days == null) continue;

            var targetDays = monthGroup.Select(d => d.Day).ToList();
            var totdDays = response.Days.Where(d => d.Map != null && targetDays.Contains(d.MonthDay)).OrderBy(d => d.MonthDay).ToList();

            if (!totdDays.Any()) continue;

            var downloadDir = Path.Combine(_defaultMapsFolder, "Track of the Day", response.Year.ToString(), response.Month.ToString("D2"));
            downloadedPaths.AddRange(await DownloadAndFixMaps(totdDays.Select(d => (d.Map!.Name, (string?)d.Map.FileName, (string?)d.Map.FileUrl, (string?)$"{d.MonthDay:D2} - ")), downloadDir, config));
        }

        return downloadedPaths;
    }

    internal List<(DateTime Start, DateTime End)> ParseToTdRanges(string input, DateTime now)
    {
        var ranges = new List<(DateTime Start, DateTime End)>();
        var parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            var rangeParts = trimmedPart.Split('-');
            if (rangeParts.Length == 1)
            {
                var r = ParseToTdDate(rangeParts[0], now);
                if (r.HasValue) ranges.Add(r.Value);
            }
            else if (rangeParts.Length == 2)
            {
                var startStr = rangeParts[0].Trim();
                var endStr = rangeParts[1].Trim();

                var start = ParseToTdDate(startStr, now);
                if (start.HasValue)
                {
                    // Special case: yyyy.mm.dd-dd
                    if (Regex.IsMatch(endStr, @"^\d{1,2}$") && int.TryParse(endStr, out var endDay))
                    {
                        var end = new DateTime(start.Value.Start.Year, start.Value.Start.Month, Math.Min(DateTime.DaysInMonth(start.Value.Start.Year, start.Value.Start.Month), endDay));
                        ranges.Add((start.Value.Start, end));
                    }
                    // Special case: yyyy.mm.dd-mm.dd
                    else if (Regex.IsMatch(endStr, @"^\d{1,2}[\.\/]\d{1,2}$"))
                    {
                        var endParts = endStr.Split(new[] { '.', '/' });
                        if (int.TryParse(endParts[0], out var endM) && int.TryParse(endParts[1], out var endD))
                        {
                             var end = new DateTime(start.Value.Start.Year, Math.Min(12, endM), 1);
                             end = new DateTime(end.Year, end.Month, Math.Min(DateTime.DaysInMonth(end.Year, end.Month), endD));
                             ranges.Add((start.Value.Start, end));
                        }
                    }
                    else
                    {
                        var end = ParseToTdDate(endStr, now);
                        if (end.HasValue)
                        {
                            if (start.Value.Start <= end.Value.End) ranges.Add((start.Value.Start, end.Value.End));
                            else ranges.Add((end.Value.Start, start.Value.End));
                        }
                    }
                }
            }
        }
        return ranges;
    }

    private (DateTime Start, DateTime End)? ParseToTdDate(string s, DateTime now)
    {
        s = s.Trim();
        // yyyy.mm.dd
        var match = Regex.Match(s, @"^(\d{4})[\.\/\-](\d{1,2})[\.\/\-](\d{1,2})$");
        if (match.Success)
        {
            int y = int.Parse(match.Groups[1].Value);
            int m = int.Parse(match.Groups[2].Value);
            int d = int.Parse(match.Groups[3].Value);
            if (m >= 1 && m <= 12)
            {
                int daysInMonth = DateTime.DaysInMonth(y, m);
                var dt = new DateTime(y, m, Math.Min(d, daysInMonth));
                return (dt, dt);
            }
        }
        // yyyy.mm
        match = Regex.Match(s, @"^(\d{4})[\.\/\-](\d{1,2})$");
        if (match.Success)
        {
            int y = int.Parse(match.Groups[1].Value);
            int m = int.Parse(match.Groups[2].Value);
            if (m >= 1 && m <= 12)
            {
                return (new DateTime(y, m, 1), new DateTime(y, m, DateTime.DaysInMonth(y, m)));
            }
        }
        // yyyy
        match = Regex.Match(s, @"^(\d{4})$");
        if (match.Success)
        {
            int y = int.Parse(match.Groups[1].Value);
            return (new DateTime(y, 1, 1), new DateTime(y, 12, 31));
        }
        return null;
    }

    public async Task<List<string>> DownloadAndFixMaps(IEnumerable<(string Name, string? FileName, string? FileUrl, string? Prefix)> maps, string downloadDir, Config config)
    {
        if (!_fs.DirectoryExists(downloadDir)) _fs.CreateDirectory(downloadDir);

        var processedPaths = new List<string>();
        var mapList = maps.ToList();
        _console.WriteLine($"Processing {mapList.Count} maps...");

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

            foreach (var c in InvalidFileNameChars) fileName = fileName.Replace(c, '_');

            if (!string.IsNullOrEmpty(prefix)) fileName = prefix + fileName;

            var filePath = Path.Combine(downloadDir, fileName);
            _console.Write($"[{i + 1}/{mapList.Count}] {deformattedName}... ");

            if (_fs.FileExists(filePath) && !config.ForceOverwrite)
            {
                _console.WriteLine("Skipped (already exists)");
                processedPaths.Add(filePath);
                continue;
            }

            try
            {
                var fileData = await _net.GetByteArrayAsync(url);
                await _fs.WriteAllBytesAsync(filePath, fileData);
                _console.Write("Downloaded and ");

                if (_fixer.ProcessFile(filePath, config))
                {
                    var fileNameOnly = Path.GetFileName(filePath);
                    var deformattedFileName = TextFormatter.Deformat(fileNameOnly);
                    if (config.DryRun) _console.WriteLine($"  [Dry Run] Would update: {deformattedFileName}");
                    else _console.WriteLine("Fixed.");
                }
                else _console.WriteLine("Saved.");

                processedPaths.Add(filePath);
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _console.WriteLine($"\n  Failed: {ex.Message}");
            }
        }
        return processedPaths;
    }

    private async Task<List<ICampaignItem>> FetchAllCampaigns(Func<int, Task<ICampaignCollection>> fetchFunc, int maxPages = int.MaxValue)
    {
        var all = new List<ICampaignItem>();
        int page = 0, pageCount = 1;
        while (page < pageCount && page < maxPages)
        {
            var response = await fetchFunc(page);
            if (response?.Campaigns != null) all.AddRange(response.Campaigns);
            pageCount = response?.PageCount ?? 0;
            page++;
        }
        return all;
    }

    public List<int> ParseNumbers(string input)
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

    internal List<(MapRef Start, MapRef End)> ParseMapRanges(string input)
    {
        var ranges = new List<(MapRef Start, MapRef End)>();
        var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var rangeParts = part.Split('-');
            if (rangeParts.Length == 1)
            {
                var mr = ParseMapRef(rangeParts[0]);
                if (mr != null) ranges.Add((mr, mr));
            }
            else if (rangeParts.Length == 2)
            {
                var start = ParseMapRef(rangeParts[0]);
                var end = ParseMapRef(rangeParts[1]);
                if (start != null && end != null)
                {
                    if (start.CompareTo(end) <= 0) ranges.Add((start, end));
                    else ranges.Add((end, start));
                }
            }
        }
        return ranges;
    }

    internal MapRef? ParseMapRef(string s)
    {
        var match = Regex.Match(s.Trim(), @"^(\d+)(?:\.(\d+))?$");
        if (match.Success)
        {
            int camp = int.Parse(match.Groups[1].Value);
            int? map = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : null;
            return new MapRef(camp, map);
        }
        return null;
    }

    internal List<(SeasonalRef Start, SeasonalRef End)> ParseSeasonalRanges(string input)
    {
        var ranges = new List<(SeasonalRef Start, SeasonalRef End)>();
        var parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            var rangeParts = Regex.Split(trimmedPart, @"\s*-\s*");
            if (rangeParts.Length == 1)
            {
                var sr = ParseSeasonalRef(rangeParts[0]);
                if (sr != null)
                {
                    if (Regex.IsMatch(rangeParts[0].Trim(), @"^\d{4}$"))
                    {
                        ranges.Add((new SeasonalRef(sr.Year, 1), new SeasonalRef(sr.Year, 4)));
                    }
                    else
                    {
                        ranges.Add((sr, sr));
                    }
                }
            }
            else if (rangeParts.Length == 2)
            {
                var start = ParseSeasonalRef(rangeParts[0]);
                var end = ParseSeasonalRef(rangeParts[1]);
                if (start != null && end != null)
                {
                    if (Regex.IsMatch(rangeParts[0].Trim(), @"^\d{4}$")) start = new SeasonalRef(start.Year, 1);
                    if (Regex.IsMatch(rangeParts[1].Trim(), @"^\d{4}$")) end = new SeasonalRef(end.Year, 4);

                    if (start.CompareTo(end) <= 0) ranges.Add((start, end));
                    else ranges.Add((end, start));
                }
            }
        }
        return ranges;
    }

    internal SeasonalRef? ParseSeasonalRef(string s)
    {
        s = s.Trim();
        var match = Regex.Match(s, @"^(Winter|Spring|Summer|Fall)\s+(\d{4})(?:\.(\d+))?$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            string season = match.Groups[1].Value;
            int year = int.Parse(match.Groups[2].Value);
            int? map = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : null;
            int order = GetSeasonOrder(season);
            return new SeasonalRef(year, order, map);
        }
        match = Regex.Match(s, @"^(\d{4})$");
        if (match.Success)
        {
            int year = int.Parse(match.Groups[1].Value);
            return new SeasonalRef(year, 1);
        }
        return null;
    }

    internal SeasonalRef? ParseSeasonalRefFromCampaignName(string campaignName)
    {
        var match = Regex.Match(campaignName, @"(Winter|Spring|Summer|Fall)\s+(\d{4})", RegexOptions.IgnoreCase);
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

    private int GetSeasonOrder(string season) => season.ToLower() switch
    {
        "winter" => 1,
        "spring" => 2,
        "summer" => 3,
        "fall" => 4,
        _ => 0
    };

    public async Task HandleExportCampaignMedals(string playerId, string? campaignNameFilter)
    {
        if (!Guid.TryParse(playerId, out var accountId))
        {
            _console.WriteLine("Error: Player ID must be a valid GUID.");
            return;
        }

        string accountIdStr = accountId.ToString();
        _console.WriteLine($"Exporting medals for Player ID: {accountIdStr}");

        _console.WriteLine("Fetching available Seasonal campaigns...");
        var allCampaigns = await FetchAllCampaigns(p => _api.GetSeasonalCampaignsAsync(p));

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

        campaignsToProcess = campaignsToProcess.OrderBy(c => c.Id).ToList();

        var csvLines = new List<string> { "Campaign Name, Track Name, Medal, Best Time" };

        for (int i = 0; i < campaignsToProcess.Count; i++)
        {
            var campaignItem = campaignsToProcess[i];
            var deformattedCampaignName = TextFormatter.Deformat(campaignItem.Name);
            _console.WriteLine($"[{i + 1}/{campaignsToProcess.Count}] Campaign: {deformattedCampaignName}");

            await Task.Delay(1000);
            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null)
            {
                _console.WriteLine("  Failed to fetch campaign details.");
                continue;
            }

            foreach (var map in fullCampaign.Playlist)
            {
                await Task.Delay(1000);
                var deformattedMapName = TextFormatter.Deformat(map.Name);
                _console.Write($"  - {deformattedMapName}... ");

                int medal = 0;
                string formattedTime = "00:00:00:000";

                try
                {
                    var leaderboard = await _api.GetLeaderboardAsync(map.MapUid, accountIdStr);
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
                catch
                {
                    _console.WriteLine("Error or no record.");
                }

                csvLines.Add($"\"{deformattedCampaignName}\", \"{deformattedMapName}\", {medal}, {formattedTime}");
            }
        }

        try
        {
            _fs.WriteAllLines("medals.csv", csvLines);
            _console.WriteLine($"\nExport complete! Saved to {Path.GetFullPath("medals.csv")}");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"\nError saving CSV: {ex.Message}");
        }
    }

    public List<string> RunBatchFixer(Config config, List<string>? extraFiles = null)
    {
        var processedPaths = new List<string>();
        var filesToProcess = new HashSet<string>();

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

        if (config.ExplicitFolder || (extraFiles == null || extraFiles.Count == 0))
        {
            if (_fs.DirectoryExists(config.FolderPath))
            {
                foreach (var file in _fs.GetFiles(config.FolderPath, "*.Map.Gbx", SearchOption.AllDirectories))
                    filesToProcess.Add(file);
            }
        }

        if (filesToProcess.Count == 0) return processedPaths;

        _console.WriteLine($"\nAnalyzing {filesToProcess.Count} maps...");
        int changed = 0;

        foreach (var file in filesToProcess)
        {
            try
            {
                if (_fixer.ProcessFile(file, config)) changed++;
                processedPaths.Add(file);
            }
            catch (Exception ex)
            {
                _console.WriteLine($"Failed to process {file}: {ex.Message}");
            }
        }

        if (config.UpdateTitle || config.ConvertPlatformMapType)
            _console.WriteLine($"\nBatch analysis complete. Analyzed: {filesToProcess.Count}, Updated: {changed}");

        return processedPaths;
    }
}

internal record MapRef(int Campaign, int? Map = null) : IComparable<MapRef>
{
    public int CompareTo(MapRef? other)
    {
        if (other == null) return 1;
        if (Campaign != other.Campaign) return Campaign.CompareTo(other.Campaign);
        if (!Map.HasValue && !other.Map.HasValue) return 0;
        if (!Map.HasValue) return -1;
        if (!other.Map.HasValue) return 1;
        return Map.Value.CompareTo(other.Map.Value);
    }
}

internal record SeasonalRef(int Year, int SeasonOrder, int? Map = null) : IComparable<SeasonalRef>
{
    public int CompareTo(SeasonalRef? other)
    {
        if (other == null) return 1;
        if (Year != other.Year) return Year.CompareTo(other.Year);
        if (SeasonOrder != other.SeasonOrder) return SeasonOrder.CompareTo(other.SeasonOrder);
        if (!Map.HasValue && !other.Map.HasValue) return 0;
        if (!Map.HasValue) return -1;
        if (!other.Map.HasValue) return 1;
        return Map.Value.CompareTo(other.Map.Value);
    }
}
