using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Serialization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Security.Cryptography;
using System.Text;
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

public record MapDownloadRecord(string Name, string? FileName, string? FileUrl, string? Prefix);

public class CacheEntry<T>
{
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CampaignCollectionDto : ICampaignCollection
{
    public List<CampaignItemDto> Campaigns { get; set; } = new();
    IEnumerable<ICampaignItem> ICampaignCollection.Campaigns => Campaigns;
    public int PageCount { get; set; }
}

public class CampaignItemDto : ICampaignItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? ClubId { get; set; }
}

public class CampaignDto : ICampaign
{
    public List<MapDto> Playlist { get; set; } = new();
    IEnumerable<IMap> ICampaign.Playlist => Playlist;
    public string Name { get; set; } = "";
    public string? ClubName { get; set; }
}

public class MapDto : IMap
{
    public string Name { get; set; } = "";
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public string MapUid { get; set; } = "";
    public TimeInt32 AuthorScore { get; set; }
    public TimeInt32 GoldScore { get; set; }
    public TimeInt32 SilverScore { get; set; }
    public TimeInt32 BronzeScore { get; set; }
}

public class TrackOfTheDayCollectionDto : ITrackOfTheDayCollection
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<TrackOfTheDayDayDto> Days { get; set; } = new();
    IEnumerable<ITrackOfTheDayDay> ITrackOfTheDayCollection.Days => Days;
}

public class TrackOfTheDayDayDto : ITrackOfTheDayDay
{
    public int MonthDay { get; set; }
    public TrackmaniaMapDto? Map { get; set; }
    ITrackmaniaMap? ITrackOfTheDayDay.Map => Map;
}

public class TrackmaniaMapDto : ITrackmaniaMap
{
    public string Name { get; set; } = "";
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
}

public class LeaderboardDto : ILeaderboard
{
    public List<RecordDto> Tops { get; set; } = new();
    IEnumerable<IRecord> ILeaderboard.Tops => Tops;
}

public class RecordDto : IRecord
{
    public TimeInt32 Time { get; set; }
}

public class TmxMapDto : ITmxMap
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public int AwardCount { get; set; }
    public int DownloadCount { get; set; }
}

public class TmxMapPackDto : ITmxMapPack
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CacheEntry<CampaignCollectionDto>))]
[JsonSerializable(typeof(CacheEntry<CampaignDto>))]
[JsonSerializable(typeof(CacheEntry<TrackOfTheDayCollectionDto>))]
[JsonSerializable(typeof(CacheEntry<LeaderboardDto>))]
[JsonSerializable(typeof(CacheEntry<TmxMapDto>))]
[JsonSerializable(typeof(CacheEntry<List<TmxMapDto>>))]
[JsonSerializable(typeof(CacheEntry<TmxMapPackDto>))]
internal partial class ToolboxCacheContext : JsonSerializerContext { }

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
    void DeleteFile(string path);
    Task WriteAllBytesAsync(string path, byte[] bytes);
    void WriteAllText(string path, string contents);
    Task WriteAllTextAsync(string path, string contents);
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path);
    string[] ReadAllLines(string path);
    void WriteAllLines(string path, IEnumerable<string> contents);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    string[] GetDirectories(string path);
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

public record BrowserItem(string DisplayName, string FullPath, bool IsDirectory)
{
    public string Icon => IsDirectory ? "📁" : "📄";
}

public interface IBrowserService
{
    IEnumerable<BrowserItem> GetBrowserItems(string directory, string filter, bool descending);
}

public interface IConfigService
{
    Task<Config> LoadConfigAsync(string scriptDirectory);
    Task SaveConfigAsync(string scriptDirectory, Config config);
    Config LoadConfig(string scriptDirectory);
}

[TomlSerializable(typeof(Config))]
internal partial class ToolboxConfigContext : TomlSerializerContext { }

public class RealConfigService : IConfigService
{
    private readonly IFileSystem _fs;

    public RealConfigService(IFileSystem fs)
    {
        _fs = fs;
    }

    public async Task<Config> LoadConfigAsync(string scriptDirectory)
    {
        var configPath = Path.Combine(scriptDirectory, "config.toml");
        if (_fs.FileExists(configPath))
        {
            try
            {
                var content = await _fs.ReadAllTextAsync(configPath);
                var config = TomlSerializer.Deserialize<Config>(content, ToolboxConfigContext.Default.Config);
                if (config != null) return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigService] ERROR: Failed to load configuration from '{configPath}'. The file might be corrupted or inaccessible. Falling back to default settings.");
                Console.WriteLine($"[ConfigService] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ConfigService] INNER EXCEPTION: {ex.InnerException.Message}");
                }
            }
        }

        return Config.Default;
    }

    public Config LoadConfig(string scriptDirectory)
    {
        var configPath = Path.Combine(scriptDirectory, "config.toml");
        if (_fs.FileExists(configPath))
        {
            try
            {
                var content = _fs.ReadAllText(configPath);
                var config = TomlSerializer.Deserialize<Config>(content, ToolboxConfigContext.Default.Config);
                if (config != null) return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load config from {configPath}. Using defaults. Error: {ex.Message}");
            }
        }

        return Config.Default;
    }

    public async Task SaveConfigAsync(string scriptDirectory, Config config)
    {
        var configPath = Path.Combine(scriptDirectory, "config.toml");
        var content = TomlSerializer.Serialize(config, ToolboxConfigContext.Default.Config);
        await _fs.WriteAllTextAsync(configPath, content);
    }
}

public class RealBrowserService : IBrowserService
{
    private readonly IFileSystem _fs;

    public RealBrowserService(IFileSystem fs)
    {
        _fs = fs;
    }

    public IEnumerable<BrowserItem> GetBrowserItems(string directory, string filter, bool descending)
    {
        if (!_fs.DirectoryExists(directory)) return Enumerable.Empty<BrowserItem>();

        List<BrowserItem> items = [];

        var dirs = _fs.GetDirectories(directory)
            .Select(d => new { Path = d, Name = Path.GetFileName(d) });

        var sortedDirs = descending ? dirs.OrderByDescending(d => d.Name) : dirs.OrderBy(d => d.Name);

        foreach (var dir in sortedDirs)
        {
            if (!string.IsNullOrEmpty(filter) && !dir.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

            items.Add(new BrowserItem(dir.Name, dir.Path, true));
        }

        var files = _fs.GetFiles(directory, "*.Map.Gbx", SearchOption.TopDirectoryOnly)
            .Select(f =>
            {
                var fn = Path.GetFileName(f);
                return new { Path = f, FileName = fn, DisplayName = TextFormatter.Deformat(fn) };
            });

        var filteredFiles = files.Where(f =>
            string.IsNullOrEmpty(filter) ||
            f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            f.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));

        var sortedFiles = descending ? filteredFiles.OrderByDescending(f => f.DisplayName) : filteredFiles.OrderBy(f => f.DisplayName);

        foreach (var file in sortedFiles)
        {
            items.Add(new BrowserItem(file.DisplayName, file.Path, false));
        }

        return items;
    }
}

public class DownloaderConfig
{
    public string? WeeklyShorts { get; set; }
    public string? WeeklyGrands { get; set; }
    public string? Seasonal { get; set; }
    public string? ClubCampaign { get; set; }
    public string? ToTDDate { get; set; }
    public string? ExportMedalsPlayerId { get; set; }
    public string? ExportMedalsCampaign { get; set; }
    public int DownloadDelayMs { get; set; } = 1000;
}

public class TmxConfig
{
    public string? TmxMaps { get; set; }
    public string? TmxPacks { get; set; }
    public string? TmxSearch { get; set; }
    public string? TmxAuthor { get; set; }
    public string TmxSort { get; set; } = "name";
    public bool TmxDesc { get; set; }
    public bool TmxRandom { get; set; }
}

public class FixerConfig
{
    public string FolderPath { get; set; } = "";
    public bool ExplicitFolder { get; set; }
    public bool UpdateTitle { get; set; } = true;
    public bool ConvertPlatformMapType { get; set; } = true;
    public bool DryRun { get; set; }
}

public class AppConfig
{
    public bool ForceOverwrite { get; set; }
    public bool Interactive { get; set; } = true;
    public bool Play { get; set; }
    public string? SetGamePath { get; set; }
    public List<string> ExtraPaths { get; set; } = new();
}

public class DesktopConfig
{
    public string BrowserFolder { get; set; } = "";
    public bool DoubleClickToPlay { get; set; } = true;
    public bool EnterToPlay { get; set; } = true;
    public bool PlayAfterDownload { get; set; }
    public bool SaveLastFolder { get; set; }
    public string LastFolder { get; set; } = "";
    public bool SaveLastSort { get; set; }
    public int LastSort { get; set; }
}

public class CacheConfig
{
    public bool Enabled { get; set; } = true;
    public int StaticExpirationMinutes { get; set; } = 43200; // 30 days
    public int DynamicExpirationMinutes { get; set; } = 60; // 1 hour
    public int HighlyDynamicExpirationMinutes { get; set; } = 5; // 5 minutes
    public string CacheDirectory { get; set; } = ".cache";
}

public class Config
{
    public DownloaderConfig Downloader { get; set; } = new();
    public TmxConfig Tmx { get; set; } = new();
    public FixerConfig Fixer { get; set; } = new();
    public AppConfig App { get; set; } = new();
    public DesktopConfig Desktop { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();

    public static Config Default
    {
        get
        {
            var config = new Config();
            var defaultMapsFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Toolbox");
            config.Fixer.FolderPath = defaultMapsFolder;
            config.Desktop.BrowserFolder = defaultMapsFolder;
            return config;
        }
    }
}

public class CachedTrackmaniaApi : ITrackmaniaApi
{
    private readonly ITrackmaniaApi _inner;
    private readonly IFileSystem _fs;
    private readonly string _cacheDir;
    private readonly CacheConfig _config;

    public CachedTrackmaniaApi(ITrackmaniaApi inner, IFileSystem fs, string scriptDirectory, CacheConfig config)
    {
        _inner = inner;
        _fs = fs;
        _config = config;
        _cacheDir = Path.IsPathRooted(config.CacheDirectory)
            ? config.CacheDirectory
            : Path.Combine(scriptDirectory, config.CacheDirectory);

        if (_config.Enabled && !_fs.DirectoryExists(_cacheDir))
        {
            _fs.CreateDirectory(_cacheDir);
        }
    }

    private async Task<T> GetCachedAsync<T>(string key, int expirationMinutes, Func<Task<T>> fetchFunc, JsonTypeInfo<CacheEntry<T>> typeInfo)
    {
        if (!_config.Enabled) return await fetchFunc();

        var cacheFile = Path.Combine(_cacheDir, $"{key}.json");
        if (_fs.FileExists(cacheFile))
        {
            try
            {
                var content = await _fs.ReadAllTextAsync(cacheFile);
                var entry = JsonSerializer.Deserialize(content, typeInfo);
                if (entry != null && (DateTime.UtcNow - entry.Timestamp).TotalMinutes < expirationMinutes)
                {
                    return entry.Data!;
                }
            }
            catch { /* Ignore cache errors and refetch */ }
        }

        var data = await fetchFunc();
        try
        {
            var entry = new CacheEntry<T> { Data = data, Timestamp = DateTime.UtcNow };
            var content = JsonSerializer.Serialize(entry, typeInfo);
            await _fs.WriteAllTextAsync(cacheFile, content);
        }
        catch { /* Ignore cache write errors */ }

        return data;
    }

    private string GetCacheKey(string rawKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public void ResetCache()
    {
        if (_fs.DirectoryExists(_cacheDir))
        {
            foreach (var file in _fs.GetFiles(_cacheDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try { _fs.DeleteFile(file); } catch { }
            }
        }
    }

    public async Task<ICampaignCollection> GetWeeklyShortCampaignsAsync(int page) =>
        await GetCachedAsync($"weekly_shorts_page_{page}", _config.DynamicExpirationMinutes,
            async () => ToCampaignCollectionDto(await _inner.GetWeeklyShortCampaignsAsync(page)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto);

    public async Task<ICampaign> GetWeeklyShortCampaignAsync(int id) =>
        await GetCachedAsync($"weekly_short_{id}", _config.StaticExpirationMinutes,
            async () => ToCampaignDto(await _inner.GetWeeklyShortCampaignAsync(id)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto);

    public async Task<ICampaignCollection> GetWeeklyGrandCampaignsAsync(int page) =>
        await GetCachedAsync($"weekly_grands_page_{page}", _config.DynamicExpirationMinutes,
            async () => ToCampaignCollectionDto(await _inner.GetWeeklyGrandCampaignsAsync(page)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto);

    public async Task<ICampaign> GetWeeklyGrandCampaignAsync(int id) =>
        await GetCachedAsync($"weekly_grand_{id}", _config.StaticExpirationMinutes,
            async () => ToCampaignDto(await _inner.GetWeeklyGrandCampaignAsync(id)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto);

    public async Task<ICampaignCollection> GetSeasonalCampaignsAsync(int page) =>
        await GetCachedAsync($"seasonal_page_{page}", _config.DynamicExpirationMinutes,
            async () => ToCampaignCollectionDto(await _inner.GetSeasonalCampaignsAsync(page)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto);

    public async Task<ICampaign> GetSeasonalCampaignAsync(int id) =>
        await GetCachedAsync($"seasonal_{id}", _config.StaticExpirationMinutes,
            async () => ToCampaignDto(await _inner.GetSeasonalCampaignAsync(id)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto);

    public async Task<ICampaignCollection> GetClubCampaignsAsync(int page) =>
        await GetCachedAsync($"club_campaigns_page_{page}", _config.DynamicExpirationMinutes,
            async () => ToCampaignCollectionDto(await _inner.GetClubCampaignsAsync(page)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto);

    public async Task<ICampaign> GetClubCampaignAsync(int clubId, int campaignId) =>
        await GetCachedAsync($"club_{clubId}_campaign_{campaignId}", _config.StaticExpirationMinutes,
            async () => ToCampaignDto(await _inner.GetClubCampaignAsync(clubId, campaignId)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto);

    public async Task<ITrackOfTheDayCollection> GetTrackOfTheDaysAsync(int monthOffset) =>
        await GetCachedAsync($"totd_offset_{monthOffset}", _config.DynamicExpirationMinutes,
            async () => ToTrackOfTheDayCollectionDto(await _inner.GetTrackOfTheDaysAsync(monthOffset)),
            ToolboxCacheContext.Default.CacheEntryTrackOfTheDayCollectionDto);

    public async Task<ILeaderboard> GetLeaderboardAsync(string mapUid, string accountId) =>
        await GetCachedAsync($"leaderboard_{mapUid}_{accountId}", _config.HighlyDynamicExpirationMinutes,
            async () => ToLeaderboardDto(await _inner.GetLeaderboardAsync(mapUid, accountId)),
            ToolboxCacheContext.Default.CacheEntryLeaderboardDto);

    public async Task<ITmxMap?> GetTmxMapAsync(int id) =>
        await GetCachedAsync($"tmx_map_{id}", _config.StaticExpirationMinutes,
            async () => ToTmxMapDto(await _inner.GetTmxMapAsync(id)),
            ToolboxCacheContext.Default.CacheEntryTmxMapDto);

    public string GetTmxMapUrl(int id) => _inner.GetTmxMapUrl(id);

    public Task<IEnumerable<ITmxMap>> SearchTmxMapsAsync(string? name, string? author, string sort, bool desc) =>
        GetCachedAsync($"tmx_search_{name}_{author}_{sort}_{desc}", _config.DynamicExpirationMinutes,
            async () => (await _inner.SearchTmxMapsAsync(name, author, sort, desc)).Select(ToTmxMapDto).ToList()!,
            ToolboxCacheContext.Default.CacheEntryListTmxMapDto).ContinueWith(t => t.Result.Cast<ITmxMap>());

    public Task<ITmxMap?> GetRandomTmxMapAsync() => _inner.GetRandomTmxMapAsync();

    public async Task<ITmxMapPack?> GetTmxMapPackAsync(int id) =>
        await GetCachedAsync($"tmx_pack_{id}", _config.StaticExpirationMinutes,
            async () => ToTmxMapPackDto(await _inner.GetTmxMapPackAsync(id)),
            ToolboxCacheContext.Default.CacheEntryTmxMapPackDto);

    public Task<IEnumerable<ITmxMap>> GetTmxMapPackMapsAsync(int id) =>
        GetCachedAsync($"tmx_pack_maps_{id}", _config.StaticExpirationMinutes,
            async () => (await _inner.GetTmxMapPackMapsAsync(id)).Select(ToTmxMapDto).ToList(),
            ToolboxCacheContext.Default.CacheEntryListTmxMapDto).ContinueWith(t => t.Result.Cast<ITmxMap>());

    public void Dispose() => _inner.Dispose();

    private CampaignCollectionDto ToCampaignCollectionDto(ICampaignCollection obj) => new()
    {
        PageCount = obj.PageCount,
        Campaigns = obj.Campaigns.Select(c => new CampaignItemDto { Id = c.Id, Name = c.Name, ClubId = c.ClubId }).ToList()
    };

    private CampaignDto ToCampaignDto(ICampaign obj) => new()
    {
        Name = obj.Name,
        ClubName = obj.ClubName,
        Playlist = obj.Playlist.Select(m => new MapDto
        {
            Name = m.Name,
            FileName = m.FileName,
            FileUrl = m.FileUrl,
            MapUid = m.MapUid,
            AuthorScore = m.AuthorScore,
            GoldScore = m.GoldScore,
            SilverScore = m.SilverScore,
            BronzeScore = m.BronzeScore
        }).ToList()
    };

    private TrackOfTheDayCollectionDto ToTrackOfTheDayCollectionDto(ITrackOfTheDayCollection obj) => new()
    {
        Year = obj.Year,
        Month = obj.Month,
        Days = obj.Days.Select(d => new TrackOfTheDayDayDto
        {
            MonthDay = d.MonthDay,
            Map = d.Map == null ? null : new TrackmaniaMapDto { Name = d.Map.Name, FileName = d.Map.FileName, FileUrl = d.Map.FileUrl }
        }).ToList()
    };

    private LeaderboardDto ToLeaderboardDto(ILeaderboard obj) => new()
    {
        Tops = obj.Tops.Select(r => new RecordDto { Time = r.Time }).ToList()
    };

    private TmxMapDto? ToTmxMapDto(ITmxMap? obj) => obj == null ? null : new TmxMapDto
    {
        Id = obj.Id,
        Name = obj.Name,
        AuthorName = obj.AuthorName,
        AwardCount = obj.AwardCount,
        DownloadCount = obj.DownloadCount
    };

    private TmxMapPackDto? ToTmxMapPackDto(ITmxMapPack? obj) => obj == null ? null : new TmxMapPackDto
    {
        Id = obj.Id,
        Name = obj.Name
    };
}

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
    public void DeleteFile(string path) => File.Delete(path);
    public Task WriteAllBytesAsync(string path, byte[] bytes) => File.WriteAllBytesAsync(path, bytes);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public Task WriteAllTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
    public string[] ReadAllLines(string path) => File.ReadAllLines(path);
    public void WriteAllLines(string path, IEnumerable<string> contents) => File.WriteAllLines(path, contents);
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);
    public string[] GetDirectories(string path) => Directory.GetDirectories(path);
}

public class RealNetworkService : INetworkService
{
    private readonly HttpClient _httpClient;
    public RealNetworkService(HttpClient httpClient) => _httpClient = httpClient;
    public Task<byte[]> GetByteArrayAsync(string url) => _httpClient.GetByteArrayAsync(url);
}

public class RealMapFixer : IMapFixer
{
    public const string LegacyTitleId = "OrbitalDev@falguiere";
    public const string TargetTitleId = "TMStadium";
    public const string LegacyMapType = "TrackMania\\TM_Platform";
    public const string TargetMapType = "TrackMania\\TM_Race";

    public bool ProcessFile(string filePath, Config cfg)
    {
        var fixerCfg = cfg.Fixer;
        if (!fixerCfg.UpdateTitle && !fixerCfg.ConvertPlatformMapType) return false;

        var gbx = Gbx.Parse<CGameCtnChallenge>(filePath);
        var map = gbx.Node;
        bool changed = false;

        if (fixerCfg.UpdateTitle && map.TitleId == LegacyTitleId)
        {
            map.TitleId = TargetTitleId;
            changed = true;
        }

        if (fixerCfg.ConvertPlatformMapType && map.MapType == LegacyMapType)
        {
            map.MapType = TargetMapType;
            changed = true;
        }

        if (changed)
        {
            if (!fixerCfg.DryRun)
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
    private readonly IConfigService _configService;
    private readonly string _scriptDirectory;
    private readonly string _defaultMapsFolder;

    public string ScriptDirectory => _scriptDirectory;
    public string DefaultMapsFolder => _defaultMapsFolder;

    public ITrackmaniaApi Api => _api;

    public ToolboxApp(ITrackmaniaApi api, IFileSystem fs, INetworkService net, IMapFixer fixer, IConsole console, IDateTime dateTime, string scriptDirectory, IConfigService? configService = null)
    {
        _api = api;
        _fs = fs;
        _net = net;
        _fixer = fixer;
        _console = console;
        _dateTime = dateTime;
        _scriptDirectory = scriptDirectory;
        _configService = configService ?? new RealConfigService(fs);
        _defaultMapsFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Toolbox");
    }

    public async Task RunAsync(Config config)
    {
        var appCfg = config.App;
        var dlCfg = config.Downloader;
        var tmxCfg = config.Tmx;
        var fixerCfg = config.Fixer;

        if (appCfg.SetGamePath != null)
        {
            await SaveGamePathAsync(appCfg.SetGamePath);
            if (!appCfg.Play && dlCfg.WeeklyShorts == null && dlCfg.WeeklyGrands == null &&
                dlCfg.Seasonal == null && dlCfg.ClubCampaign == null && dlCfg.ToTDDate == null &&
                dlCfg.ExportMedalsPlayerId == null && !fixerCfg.ExplicitFolder && appCfg.ExtraPaths.Count == 0)
                return;
        }

        List<string> mapPaths = [];
        bool downloadActionTaken = false;

        if (dlCfg.WeeklyShorts != null)
        {
            mapPaths.AddRange(await HandleWeeklyShorts(dlCfg.WeeklyShorts, config));
            downloadActionTaken = true;
        }

        if (dlCfg.WeeklyGrands != null)
        {
            mapPaths.AddRange(await HandleWeeklyGrands(dlCfg.WeeklyGrands, config));
            downloadActionTaken = true;
        }

        if (dlCfg.Seasonal != null)
        {
            mapPaths.AddRange(await HandleSeasonal(dlCfg.Seasonal, config));
            downloadActionTaken = true;
        }

        if (dlCfg.ClubCampaign != null)
        {
            mapPaths.AddRange(await HandleClubCampaign(dlCfg.ClubCampaign, config));
            downloadActionTaken = true;
        }

        if (dlCfg.ToTDDate != null)
        {
            mapPaths.AddRange(await HandleTrackOfTheDay(dlCfg.ToTDDate, config));
            downloadActionTaken = true;
        }

        if (tmxCfg.TmxMaps != null)
        {
            mapPaths.AddRange(await HandleTmxMaps(tmxCfg.TmxMaps, config));
            downloadActionTaken = true;
        }

        if (tmxCfg.TmxPacks != null)
        {
            mapPaths.AddRange(await HandleTmxPacks(tmxCfg.TmxPacks, config));
            downloadActionTaken = true;
        }

        if (tmxCfg.TmxSearch != null || tmxCfg.TmxAuthor != null)
        {
            mapPaths.AddRange(await HandleTmxSearch(tmxCfg.TmxSearch, tmxCfg.TmxAuthor, tmxCfg.TmxSort, tmxCfg.TmxDesc, config));
            downloadActionTaken = true;
        }

        if (tmxCfg.TmxRandom)
        {
            mapPaths.AddRange(await HandleTmxRandom(config));
            downloadActionTaken = true;
        }

        if (dlCfg.ExportMedalsPlayerId != null)
        {
            await HandleExportCampaignMedals(dlCfg.ExportMedalsPlayerId, dlCfg.ExportMedalsCampaign, config);
            downloadActionTaken = true;
        }

        if (fixerCfg.ExplicitFolder || appCfg.ExtraPaths.Count > 0 || (!downloadActionTaken && appCfg.SetGamePath == null))
        {
            var fixedPaths = RunBatchFixer(config, appCfg.ExtraPaths);
            foreach (var path in fixedPaths)
            {
                if (!mapPaths.Contains(path)) mapPaths.Add(path);
            }
        }

        if (appCfg.Play)
        {
            LaunchGame(mapPaths);
        }
    }

    private async Task SaveGamePathAsync(string path)
    {
        try
        {
            var config = await _configService.LoadConfigAsync(_scriptDirectory);
            config.App.SetGamePath = path;
            await _configService.SaveConfigAsync(_scriptDirectory, config);
            _console.WriteLine($"Game path saved to: {Path.Combine(_scriptDirectory, "config.toml")}");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    private string? GetGamePath()
    {
        var config = _configService.LoadConfig(_scriptDirectory);
        return config.App.SetGamePath;
    }

    public void LaunchGame(List<string> mapPaths)
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
        List<string> downloadedPaths = [];
        _console.WriteLine($"Fetching available {displayName} campaigns...");
        var allCampaigns = await FetchAllCampaigns(fetchAllFunc);

        var campaignWithNums = allCampaigns
            .Select(c =>
            {
                var match = Regex.Match(c.Name, idRegexPattern, RegexOptions.IgnoreCase);
                return new { Campaign = c, Num = match.Success ? int.Parse(match.Groups[1].Value) : -1 };
            })
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
            ranges = ParseMapRanges(input);
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
            downloadedPaths.AddRange(await DownloadAndFixMaps(
                mapsToDownload.Select(m => new MapDownloadRecord(m.map.Name, m.map.FileName, m.map.FileUrl, prefixFunc(m.map, m.index, weekIdStr))),
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
        List<string> downloadedPaths = [];
        _console.WriteLine("Fetching available Seasonal campaigns...");
        var allCampaigns = await FetchAllCampaigns(p => _api.GetSeasonalCampaignsAsync(p));

        var campaignRefs = allCampaigns
            .Select(c => new { Campaign = c, Ref = ParseSeasonalRefFromCampaignName(c.Name) })
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
            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null) return downloadedPaths;

            var seasonalFolderName = SanitizeFolderName(FormatSeasonalFolderName(campaignItem.Name));
            var downloadDir = Path.Combine(_defaultMapsFolder, "Seasonal", seasonalFolderName);
            return await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => new MapDownloadRecord(m.Name, m.FileName, m.FileUrl, $"{(i + 1):D2} - ")), downloadDir, config);
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

            var seasonalFolderName = SanitizeFolderName(FormatSeasonalFolderName(campaignItem.Name));
            var downloadDir = Path.Combine(_defaultMapsFolder, "Seasonal", seasonalFolderName);
            downloadedPaths.AddRange(await DownloadAndFixMaps(
                mapsToDownload.Select(m => new MapDownloadRecord(m.map.Name, m.map.FileName, m.map.FileUrl, $"{(m.index + 1):D2} - ")),
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
                return [];
            }

            List<string> downloadedPaths = [];
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
            else if (matches.Count == 1 || !config.App.Interactive)
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
        return [];
    }

    private async Task<List<string>> DownloadClubCampaign(int clubId, int campaignId, Config config)
    {
        var fullCampaign = await _api.GetClubCampaignAsync(clubId, campaignId);
        if (fullCampaign?.Playlist == null) return [];

        var clubPart = SanitizeFolderName(!string.IsNullOrEmpty(fullCampaign.ClubName) ? fullCampaign.ClubName : clubId.ToString());
        var campaignPart = SanitizeFolderName(!string.IsNullOrEmpty(fullCampaign.Name) ? fullCampaign.Name : campaignId.ToString());

        var downloadDir = Path.Combine(_defaultMapsFolder, "Clubs", clubPart, campaignPart);
        return await DownloadAndFixMaps(fullCampaign.Playlist.Select((m, i) => new MapDownloadRecord(m.Name, m.FileName, m.FileUrl, $"{(i + 1):D2} - ")), downloadDir, config);
    }

    public async Task<List<string>> HandleTmxMaps(string input, Config config)
    {
        List<string> downloadedPaths = [];
        var ids = ParseTmxIds(input);
        if (!ids.Any()) return downloadedPaths;

        var downloadDir = Path.Combine(_defaultMapsFolder, "Exchange");
        List<MapDownloadRecord> mapsToDownload = [];

        foreach (var id in ids)
        {
            var map = await _api.GetTmxMapAsync(id);
            if (map != null)
            {
                mapsToDownload.Add(new MapDownloadRecord(map.Name, null, _api.GetTmxMapUrl(map.Id), null));
            }
            else
            {
                _console.WriteLine($"Error: Could not find TMX map {id}.");
            }
        }

        return await DownloadAndFixMaps(mapsToDownload, downloadDir, config);
    }

    public async Task<List<string>> HandleTmxPacks(string input, Config config)
    {
        List<string> downloadedPaths = [];
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

            var deformattedPackName = TextFormatter.Deformat(pack.Name);
            _console.WriteLine($"Found Map Pack: {deformattedPackName}");
            var maps = await _api.GetTmxMapPackMapsAsync(pack.Id);
            var downloadDir = Path.Combine(_defaultMapsFolder, "Exchange", SanitizeFolderName(deformattedPackName));

            downloadedPaths.AddRange(await DownloadAndFixMaps(maps.Select((m, i) => new MapDownloadRecord(m.Name, null, _api.GetTmxMapUrl(m.Id), $"{(i + 1):D2} - ")), downloadDir, config));
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
            return [];
        }

        if (!config.App.Interactive)
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

        return [];
    }

    public async Task<List<string>> HandleTmxRandom(Config config)
    {
        _console.WriteLine("Fetching random map from TMX...");
        var map = await _api.GetRandomTmxMapAsync();
        if (map == null)
        {
            _console.WriteLine("Error: Failed to fetch random map.");
            return [];
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
            ranges = ParseToTdRanges(dateInput, now);
        }

        if (!ranges.Any()) return [];

        List<string> downloadedPaths = [];
        List<DateTime> allDaysToDownload = [];
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
            downloadedPaths.AddRange(await DownloadAndFixMaps(totdDays.Select(d => new MapDownloadRecord(d.Map!.Name, d.Map.FileName, d.Map.FileUrl, $"{d.MonthDay:D2} - ")), downloadDir, config));
        }

        return downloadedPaths;
    }

    internal List<(DateTime Start, DateTime End)> ParseToTdRanges(string input, DateTime now)
    {
        List<(DateTime Start, DateTime End)> ranges = [];
        ForEachRange(input, new[] { ',' }, (s, e) =>
        {
            var start = ParseToTdDate(s, now);
            if (!start.HasValue) return;

            if (e == null)
            {
                ranges.Add(start.Value);
            }
            else
            {
                var endStr = e.Trim();
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
        });
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

    public async Task<List<string>> DownloadAndFixMaps(IEnumerable<MapDownloadRecord> maps, string downloadDir, Config config)
    {
        if (!_fs.DirectoryExists(downloadDir)) _fs.CreateDirectory(downloadDir);

        var processedPaths = new List<string>();
        var mapList = maps.ToList();
        _console.WriteLine($"Processing {mapList.Count} maps...");

        for (int i = 0; i < mapList.Count; i++)
        {
            var map = mapList[i];
            if (string.IsNullOrEmpty(map.FileUrl)) continue;

            var name = map.Name;
            var rawFileName = map.FileName;
            var url = map.FileUrl;
            var prefix = map.Prefix;

            var fileName = (rawFileName ?? name).Trim();
            var deformattedName = TextFormatter.Deformat(name);
            fileName = TextFormatter.Deformat(fileName);

            string extension = "";
            if (fileName.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".Map.Gbx";
                fileName = fileName[..^8].Trim();
            }
            else if (fileName.EndsWith(".Gbx", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".Gbx";
                fileName = fileName[..^4].Trim();
            }
            else
            {
                fileName = fileName.Trim();
                extension = ".Map.Gbx";
            }
            fileName += extension;

            foreach (var c in InvalidFileNameChars) fileName = fileName.Replace(c, '_');

            if (!string.IsNullOrEmpty(prefix)) fileName = prefix + fileName;

            var filePath = Path.Combine(downloadDir, fileName);
            _console.Write($"[{i + 1}/{mapList.Count}] {deformattedName}... ");

            if (_fs.FileExists(filePath) && !config.App.ForceOverwrite)
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
                    if (config.Fixer.DryRun) _console.WriteLine($"  [Dry Run] Would update: {deformattedFileName}");
                    else _console.WriteLine("Fixed.");
                }
                else _console.WriteLine("Saved.");

                processedPaths.Add(filePath);
                await Task.Delay(config.Downloader.DownloadDelayMs);
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
        List<ICampaignItem> all = [];
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

    private void ForEachRange(string input, char[] separators, Action<string, string?> action)
    {
        var parts = input.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            var rangeParts = Regex.Split(trimmedPart, @"\s*-\s*");
            if (rangeParts.Length == 1) action(rangeParts[0], null);
            else if (rangeParts.Length == 2) action(rangeParts[0], rangeParts[1]);
        }
    }

    internal List<(MapRef Start, MapRef End)> ParseMapRanges(string input)
    {
        List<(MapRef Start, MapRef End)> ranges = [];
        ForEachRange(input, new[] { ',', ' ' }, (s, e) =>
        {
            var start = ParseMapRef(s);
            if (start == null) return;

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
            }
        });
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
        List<(SeasonalRef Start, SeasonalRef End)> ranges = [];
        ForEachRange(input, new[] { ',' }, (s, e) =>
        {
            var start = ParseSeasonalRef(s);
            if (start == null) return;

            if (e == null)
            {
                if (Regex.IsMatch(s.Trim(), @"^\d{4}$"))
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
                    if (Regex.IsMatch(s.Trim(), @"^\d{4}$")) finalStart = new SeasonalRef(start.Year, 1);
                    if (Regex.IsMatch(e.Trim(), @"^\d{4}$")) finalEnd = new SeasonalRef(end.Year, 4);

                    if (finalStart.CompareTo(finalEnd) <= 0) ranges.Add((finalStart, finalEnd));
                    else ranges.Add((finalEnd, finalStart));
                }
            }
        });
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

    private string SanitizeFolderName(string folderName)
    {
        var sanitized = folderName;
        foreach (var c in InvalidFileNameChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }
        return sanitized.Trim();
    }

    public async Task HandleExportCampaignMedals(string playerId, string? campaignNameFilter, Config config)
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

        List<string> csvLines = ["Campaign Name, Track Name, Medal, Best Time"];

        for (int i = 0; i < campaignsToProcess.Count; i++)
        {
            var campaignItem = campaignsToProcess[i];
            var deformattedCampaignName = TextFormatter.Deformat(campaignItem.Name);
            _console.WriteLine($"[{i + 1}/{campaignsToProcess.Count}] Campaign: {deformattedCampaignName}");

            await Task.Delay(config.Downloader.DownloadDelayMs);
            var fullCampaign = await _api.GetSeasonalCampaignAsync(campaignItem.Id);
            if (fullCampaign?.Playlist == null)
            {
                _console.WriteLine("  Failed to fetch campaign details.");
                continue;
            }

            foreach (var map in fullCampaign.Playlist)
            {
                await Task.Delay(config.Downloader.DownloadDelayMs);
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
                catch (Exception ex)
                {
                    _console.WriteLine($"Error: {ex.Message}");
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
        var processedPaths = new ConcurrentBag<string>();
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

        Parallel.ForEach(filesToProcess, file =>
        {
            try
            {
                if (_fixer.ProcessFile(file, config))
                {
                    Interlocked.Increment(ref changed);
                }
                processedPaths.Add(file);
            }
            catch (Exception ex)
            {
                _console.WriteLine($"Failed to process {file}: {ex.Message}");
            }
        });

        if (config.Fixer.UpdateTitle || config.Fixer.ConvertPlatformMapType)
            _console.WriteLine($"\nBatch analysis complete. Analyzed: {filesToProcess.Count}, Updated: {changed}");

        return processedPaths.ToList();
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
