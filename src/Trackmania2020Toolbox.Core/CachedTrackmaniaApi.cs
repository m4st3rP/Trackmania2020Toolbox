using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Trackmania2020Toolbox;

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

        var hashedKey = GetCacheKey(key);
        var cacheFile = Path.Combine(_cacheDir, $"{hashedKey}.json");
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

    private static string GetCacheKey(string rawKey)
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

    public async Task<IEnumerable<ITmxMap>> SearchTmxMapsAsync(string? name, string? author, string sort, bool desc)
    {
        var list = await GetCachedAsync($"tmx_search_{name}_{author}_{sort}_{desc}", _config.DynamicExpirationMinutes,
            async () => (await _inner.SearchTmxMapsAsync(name, author, sort, desc)).Select(ToTmxMapDto).ToList()!,
            ToolboxCacheContext.Default.CacheEntryListTmxMapDto);
        return list.Cast<ITmxMap>();
    }

    public Task<ITmxMap?> GetRandomTmxMapAsync() => _inner.GetRandomTmxMapAsync();

    public async Task<ITmxMapPack?> GetTmxMapPackAsync(int id) =>
        await GetCachedAsync($"tmx_pack_{id}", _config.StaticExpirationMinutes,
            async () => ToTmxMapPackDto(await _inner.GetTmxMapPackAsync(id)),
            ToolboxCacheContext.Default.CacheEntryTmxMapPackDto);

    public async Task<IEnumerable<ITmxMap>> GetTmxMapPackMapsAsync(int id)
    {
        var list = await GetCachedAsync($"tmx_pack_maps_{id}", _config.StaticExpirationMinutes,
            async () => (await _inner.GetTmxMapPackMapsAsync(id)).Select(ToTmxMapDto).ToList(),
            ToolboxCacheContext.Default.CacheEntryListTmxMapDto);
        return list.Cast<ITmxMap>();
    }

    public void Dispose()
    {
        _inner.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CampaignCollectionDto ToCampaignCollectionDto(ICampaignCollection obj) => new()
    {
        PageCount = obj.PageCount,
        Campaigns = obj.Campaigns.Select(c => new CampaignItemDto { Id = c.Id, Name = c.Name, ClubId = c.ClubId }).ToList()
    };

    private static CampaignDto ToCampaignDto(ICampaign obj) => new()
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

    private static TrackOfTheDayCollectionDto ToTrackOfTheDayCollectionDto(ITrackOfTheDayCollection obj) => new()
    {
        Year = obj.Year,
        Month = obj.Month,
        Days = obj.Days.Select(d => new TrackOfTheDayDayDto
        {
            MonthDay = d.MonthDay,
            Map = d.Map == null ? null : new TrackmaniaMapDto { Name = d.Map.Name, FileName = d.Map.FileName, FileUrl = d.Map.FileUrl }
        }).ToList()
    };

    private static LeaderboardDto ToLeaderboardDto(ILeaderboard obj) => new()
    {
        Tops = obj.Tops.Select(r => new RecordDto { Time = r.Time }).ToList()
    };

    private static TmxMapDto? ToTmxMapDto(ITmxMap? obj) => obj == null ? null : new TmxMapDto
    {
        Id = obj.Id,
        Name = obj.Name,
        AuthorName = obj.AuthorName,
        AwardCount = obj.AwardCount,
        DownloadCount = obj.DownloadCount
    };

    private static TmxMapPackDto? ToTmxMapPackDto(ITmxMapPack? obj) => obj == null ? null : new TmxMapPackDto
    {
        Id = obj.Id,
        Name = obj.Name
    };
}
