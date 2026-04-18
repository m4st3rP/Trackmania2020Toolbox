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
}

public record Config(
    string? WeeklyShorts, string? WeeklyGrands, string? Seasonal, string? ClubCampaign, string? ToTDDate,
    string? ExportMedalsPlayerId, string? ExportMedalsCampaign,
    string FolderPath, bool ExplicitFolder, bool UpdateTitle, bool ConvertPlatformMapType, bool DryRun,
    bool ForceOverwrite, bool Interactive, bool Play, string? SetGamePath, List<string> ExtraPaths
);

public class TrackmaniaApiWrapper : ITrackmaniaApi
{
    private readonly TrackmaniaIO _api;
    public TrackmaniaApiWrapper(string userAgent) => _api = new TrackmaniaIO(userAgent);

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

    public void Dispose() => _api.Dispose();

    private class CampaignCollectionProxy : ICampaignCollection
    {
        private readonly dynamic _obj;
        public CampaignCollectionProxy(object obj) => _obj = obj;
        public IEnumerable<ICampaignItem> Campaigns => ((IEnumerable<dynamic>)_obj.Campaigns).Select(c => (ICampaignItem)new CampaignItemProxy(c));
        public int PageCount => _obj.PageCount;
    }

    private class CampaignItemProxy : ICampaignItem
    {
        private readonly dynamic _obj;
        public CampaignItemProxy(object obj) => _obj = obj;
        public int Id => _obj.Id;
        public string Name => _obj.Name;
        public int? ClubId => _obj.ClubId;
    }

    private class CampaignProxy : ICampaign
    {
        private readonly dynamic _obj;
        public CampaignProxy(object obj) => _obj = obj;
        public IEnumerable<IMap> Playlist => ((IEnumerable<dynamic>)_obj.Playlist).Select(m => (IMap)new MapProxy(m));
        public string Name => _obj.Name;
        public string? ClubName => _obj.ClubName;
    }

    private class MapProxy : IMap
    {
        private readonly dynamic _obj;
        public MapProxy(object obj) => _obj = obj;
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
        private readonly object _obj;
        public TrackOfTheDayCollectionProxy(object obj) => _obj = obj;
        public int Year => (int)_obj.GetType().GetProperty("Year")!.GetValue(_obj)!;
        public int Month => (int)_obj.GetType().GetProperty("Month")!.GetValue(_obj)!;
        public IEnumerable<ITrackOfTheDayDay> Days => ((IEnumerable<object>)_obj.GetType().GetProperty("Days")!.GetValue(_obj)!).Select(d => (ITrackOfTheDayDay)new TrackOfTheDayDayProxy(d));
    }

    private class TrackOfTheDayDayProxy : ITrackOfTheDayDay
    {
        private readonly object _obj;
        public TrackOfTheDayDayProxy(object obj) => _obj = obj;
        public int MonthDay => (int)_obj.GetType().GetProperty("MonthDay")!.GetValue(_obj)!;
        public ITrackmaniaMap? Map
        {
            get
            {
                var map = _obj.GetType().GetProperty("Map")!.GetValue(_obj);
                return map != null ? new TrackmaniaMapProxy(map) : null;
            }
        }
    }

    private class TrackmaniaMapProxy : ITrackmaniaMap
    {
        private readonly object _obj;
        public TrackmaniaMapProxy(object obj) => _obj = obj;
        public string Name => (string)_obj.GetType().GetProperty("Name")!.GetValue(_obj)!;
        public string? FileName => (string?)_obj.GetType().GetProperty("FileName")!.GetValue(_obj);
        public string? FileUrl => (string?)_obj.GetType().GetProperty("FileUrl")!.GetValue(_obj);
    }

    private class LeaderboardProxy : ILeaderboard
    {
        private readonly dynamic _obj;
        public LeaderboardProxy(object obj) => _obj = obj;
        public IEnumerable<IRecord> Tops => ((IEnumerable<dynamic>)_obj.Tops).Select(r => (IRecord)new RecordProxy(r));
    }

    private class RecordProxy : IRecord
    {
        private readonly dynamic _obj;
        public RecordProxy(object obj) => _obj = obj;
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

public class RealConsole : IConsole
{
    public void WriteLine(string? value = null) => Console.WriteLine(value);
    public void Write(string? value = null) => Console.Write(value);
    public string? ReadLine() => Console.ReadLine();
}

public class ToolboxApp
{
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
        _defaultMapsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Toolbox");
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

    public async Task<List<string>> HandleWeeklyShorts(string input, Config config)
    {
        var downloadedPaths = new List<string>();
        _console.WriteLine("Fetching available Weekly Shorts campaigns...");
        var campaigns = await FetchAllCampaigns(p => _api.GetWeeklyShortCampaignsAsync(p));

        var requestedWeeks = input.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new List<int> { -1 }
            : ParseNumbers(input);

        if (!requestedWeeks.Any()) return downloadedPaths;

        foreach (var weekNum in requestedWeeks)
        {
            ICampaignItem? campaignItem;
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
                _console.WriteLine($"Error: Could not find campaign {(weekNum == -1 ? "latest" : $"Week {weekNum}")}. Skipping.");
                continue;
            }

            _console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

            var fullCampaign = await _api.GetWeeklyShortCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var weekIdStr = weekNum == -1 ? Regex.Match(campaignItem.Name, @"\bWeek 0*(\d+)\b", RegexOptions.IgnoreCase).Groups[1].Value : weekNum.ToString();
            if (string.IsNullOrEmpty(weekIdStr)) weekIdStr = campaignItem.Id.ToString();

            var downloadDir = Path.Combine(_defaultMapsFolder, "Weekly Shorts", weekIdStr);
            downloadedPaths.AddRange(await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config));
        }
        return downloadedPaths;
    }

    public async Task<List<string>> HandleWeeklyGrands(string input, Config config)
    {
        var downloadedPaths = new List<string>();
        _console.WriteLine("Fetching available Weekly Grands campaigns...");
        var campaigns = await FetchAllCampaigns(p => _api.GetWeeklyGrandCampaignsAsync(p));

        var requestedWeeks = input.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new List<int> { -1 }
            : ParseNumbers(input);

        if (!requestedWeeks.Any()) return downloadedPaths;

        foreach (var weekNum in requestedWeeks)
        {
            ICampaignItem? campaignItem;
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
                _console.WriteLine($"Error: Could not find campaign {(weekNum == -1 ? "latest" : $"Week Grand {weekNum}")}. Skipping.");
                continue;
            }

            _console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

            var fullCampaign = await _api.GetWeeklyGrandCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) continue;

            var weekIdStr = weekNum == -1 ? Regex.Match(campaignItem.Name, @"\bWeek Grand 0*(\d+)\b", RegexOptions.IgnoreCase).Groups[1].Value : weekNum.ToString();
            if (string.IsNullOrEmpty(weekIdStr)) weekIdStr = campaignItem.Id.ToString();

            var downloadDir = Path.Combine(_defaultMapsFolder, "Weekly Grands");
            downloadedPaths.AddRange(await DownloadAndFixMaps(fullCampaign.Playlist.Select(m => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{weekIdStr} - ")), downloadDir, config));
        }
        return downloadedPaths;
    }

    public async Task<List<string>> HandleSeasonal(string input, Config config)
    {
        _console.WriteLine("Fetching available Seasonal campaigns...");
        var campaigns = await FetchAllCampaigns(p => _api.GetSeasonalCampaignsAsync(p));

        ICampaignItem? campaignItem;
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
            _console.WriteLine($"Error: Could not find seasonal campaign matching '{input}'.");
            return new List<string>();
        }

        _console.WriteLine($"Found: {TextFormatter.Deformat(campaignItem.Name)}");

        var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id);
        if (fullCampaign?.Playlist == null) return new List<string>();

        var seasonalFolderName = FormatSeasonalFolderName(campaignItem.Name);
        var downloadDir = Path.Combine(_defaultMapsFolder, "Seasonal", seasonalFolderName);
        return await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => (m.Name, (string?)m.FileName, (string?)m.FileUrl, (string?)$"{(i + 1):D2} - ")), downloadDir, config);
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
        else
        {
            _console.WriteLine($"Searching for club campaigns matching '{input}'...");
            var campaigns = await FetchAllCampaigns(p => _api.GetClubCampaignsAsync(p));
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
                _console.WriteLine("\nMultiple campaigns found:");
                for (int i = 0; i < matches.Count; i++)
                {
                    _console.WriteLine($"{i + 1}: {TextFormatter.Deformat(matches[i].Name)} (ID: {matches[i].ClubId}/{matches[i].Id})");
                }
                _console.Write("\nSelect a campaign number (or 0 to cancel): ");
                if (int.TryParse(_console.ReadLine(), out var choice) && choice > 0 && choice <= matches.Count)
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

    public async Task<List<string>> HandleTrackOfTheDay(string dateInput, Config config)
    {
        var now = _dateTime.UtcNow;
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
                        // Whole month
                    }
                    else if (parts.Length == 3)
                    {
                        if (int.TryParse(parts[2], out var d)) requestedDays.Add(d);
                    }
                    else if (parts.Length >= 4)
                    {
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
        var response = await _api.GetTrackOfTheDaysAsync(monthOffset);
        if (response?.Days == null) return new List<string>();

        var totdDays = response.Days.Where(d => d.Map != null);
        if (requestedDays.Any())
        {
            var filteredDays = totdDays.Where(d => requestedDays.Contains(d.MonthDay)).ToList();

            if (isLatest && !filteredDays.Any() && now.Hour < 17)
            {
                var yesterday = now.AddDays(-1);
                int yesterdayMonthOffset = (now.Year - yesterday.Year) * 12 + (now.Month - yesterday.Month);

                var yesterdayResponse = (yesterdayMonthOffset == monthOffset) ? response : await _api.GetTrackOfTheDaysAsync(yesterdayMonthOffset);

                if (yesterdayResponse?.Days != null)
                {
                    filteredDays = yesterdayResponse.Days.Where(d => d.Map != null && d.MonthDay == yesterday.Day).ToList();
                    if (filteredDays.Any())
                    {
                        var downloadDirYesterday = Path.Combine(_defaultMapsFolder, "Track of the Day", yesterdayResponse.Year.ToString(), yesterdayResponse.Month.ToString("D2"));
                        return await DownloadAndFixMaps(filteredDays.Select(d => (d.Map!.Name, (string?)d.Map.FileName, (string?)d.Map.FileUrl, (string?)$"{d.MonthDay:D2} - ")), downloadDirYesterday, config);
                    }
                }
            }

            totdDays = filteredDays;
        }

        if (!totdDays.Any()) return new List<string>();

        var downloadDir = Path.Combine(_defaultMapsFolder, "Track of the Day", response.Year.ToString(), response.Month.ToString("D2"));
        return await DownloadAndFixMaps(totdDays.Select(d => (d.Map!.Name, (string?)d.Map.FileName, (string?)d.Map.FileUrl, (string?)$"{d.MonthDay:D2} - ")), downloadDir, config);
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

            foreach (var c in Path.GetInvalidFileNameChars().Union(new[] { '/', '\\', ':', '*', '?', '\"', '<', '>', '|' })) fileName = fileName.Replace(c, '_');

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

    private async Task<List<ICampaignItem>> FetchAllCampaigns(Func<int, Task<ICampaignCollection>> fetchFunc)
    {
        var all = new List<ICampaignItem>();
        int page = 0, pageCount = 1;
        while (page < pageCount)
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

internal static class Trackmania2020Toolbox
{
    private static readonly string UserAgent = "Trackmania2020Toolbox/1.0 (contact: trackmania-downloader-script@example.com)";
    private static readonly HttpClient HttpClient = new HttpClient();

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

        using var api = new TrackmaniaApiWrapper(UserAgent);
        var fs = new RealFileSystem();
        var net = new RealNetworkService(HttpClient);
        var fixer = new RealMapFixer();
        var console = new RealConsole();
        var dateTime = new RealDateTime();

        var app = new ToolboxApp(api, fs, net, fixer, console, dateTime, GetScriptDirectory());
        await app.RunAsync(config);
    }

    public static Config ParseArguments(string[] args)
    {
        string? weeklyShorts = null;
        string? weeklyGrands = null;
        string? seasonal = null;
        string? clubCampaign = null;
        string? totdDate = null;
        string? exportMedalsPlayerId = null;
        string? exportMedalsCampaign = null;
        string defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Toolbox");
        string folder = defaultFolder;
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

    public static void PrintUsage()
    {
        Console.WriteLine("Trackmania 2020 Toolbox");
        Console.WriteLine("Usage: dotnet run --project src/Trackmania2020Toolbox.csproj -- [options] [maps/folders...]");
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
}
