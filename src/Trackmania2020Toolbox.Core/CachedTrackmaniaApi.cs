using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

namespace Trackmania2020Toolbox;

public class CachedTrackmaniaApi : ITrackmaniaApi
{
    private readonly ITrackmaniaApi _inner;
    private readonly IFileSystem _fs;
    private readonly IDateTime _dateTime;
    private readonly string _cacheDir;
    private readonly CacheConfig _config;

    public CachedTrackmaniaApi(ITrackmaniaApi inner, IFileSystem fs, IDateTime dateTime, string scriptDirectory, CacheConfig config)
    {
        _inner = inner;
        _fs = fs;
        _dateTime = dateTime;
        _config = config;
        _cacheDir = Path.IsPathRooted(config.CacheDirectory)
            ? config.CacheDirectory
            : Path.Combine(scriptDirectory, config.CacheDirectory);

        if (_config.Enabled && !_fs.DirectoryExists(_cacheDir))
        {
            _fs.CreateDirectory(_cacheDir);
        }
    }

    private async Task<T> GetCachedAsync<T>(string key, int expirationMinutes, Func<CancellationToken, Task<T>> fetchFunc, JsonTypeInfo<CacheEntry<T>> typeInfo, CancellationToken ct)
    {
        if (!_config.Enabled) return await fetchFunc(ct);

        var hashedKey = GetCacheKey(key);
        var cacheFile = Path.Combine(_cacheDir, $"{hashedKey}.json");
        if (_fs.FileExists(cacheFile))
        {
            try
            {
                var content = await _fs.ReadAllTextAsync(cacheFile, ct);
                var entry = JsonSerializer.Deserialize(content, typeInfo);
                if (entry != null && (_dateTime.UtcNow - entry.Timestamp).TotalMinutes < expirationMinutes)
                {
                    return entry.Data!;
                }
            }
            catch { /* Ignore cache errors and refetch */ }
        }

        var data = await fetchFunc(ct);
        try
        {
            var entry = new CacheEntry<T> { Data = data, Timestamp = _dateTime.UtcNow };
            var content = JsonSerializer.Serialize(entry, typeInfo);
            await _fs.WriteAllTextAsync(cacheFile, content, ct);
        }
        catch { /* Ignore cache write errors */ }

        return data;
    }

    private static string GetCacheKey(string rawKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public int DelayMs
    {
        get => _inner.DelayMs;
        set => _inner.DelayMs = value;
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

    public async Task<ICampaignCollection> GetWeeklyShortCampaignsAsync(int page, CancellationToken ct = default) =>
        await GetCachedAsync($"weekly_shorts_page_{page}", _config.DynamicExpirationMinutes,
            async (c) => ToCampaignCollectionDto(await _inner.GetWeeklyShortCampaignsAsync(page, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto, ct);

    public async Task<ICampaign> GetWeeklyShortCampaignAsync(int id, CancellationToken ct = default) =>
        await GetCachedAsync($"weekly_short_{id}", _config.StaticExpirationMinutes,
            async (c) => ToCampaignDto(await _inner.GetWeeklyShortCampaignAsync(id, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto, ct);

    public async Task<ICampaignCollection> GetWeeklyGrandCampaignsAsync(int page, CancellationToken ct = default) =>
        await GetCachedAsync($"weekly_grands_page_{page}", _config.DynamicExpirationMinutes,
            async (c) => ToCampaignCollectionDto(await _inner.GetWeeklyGrandCampaignsAsync(page, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto, ct);

    public async Task<ICampaign> GetWeeklyGrandCampaignAsync(int id, CancellationToken ct = default) =>
        await GetCachedAsync($"weekly_grand_{id}", _config.StaticExpirationMinutes,
            async (c) => ToCampaignDto(await _inner.GetWeeklyGrandCampaignAsync(id, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto, ct);

    public async Task<ICampaignCollection> GetSeasonalCampaignsAsync(int page, CancellationToken ct = default) =>
        await GetCachedAsync($"seasonal_page_{page}", _config.DynamicExpirationMinutes,
            async (c) => ToCampaignCollectionDto(await _inner.GetSeasonalCampaignsAsync(page, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto, ct);

    public async Task<ICampaign> GetSeasonalCampaignAsync(int id, CancellationToken ct = default) =>
        await GetCachedAsync($"seasonal_{id}", _config.StaticExpirationMinutes,
            async (c) => ToCampaignDto(await _inner.GetSeasonalCampaignAsync(id, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto, ct);

    public async Task<ICampaignCollection> GetClubCampaignsAsync(int page, CancellationToken ct = default) =>
        await GetCachedAsync($"club_campaigns_page_{page}", _config.DynamicExpirationMinutes,
            async (c) => ToCampaignCollectionDto(await _inner.GetClubCampaignsAsync(page, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignCollectionDto, ct);

    public async Task<ICampaign> GetClubCampaignAsync(int clubId, int campaignId, CancellationToken ct = default) =>
        await GetCachedAsync($"club_{clubId}_campaign_{campaignId}", _config.StaticExpirationMinutes,
            async (c) => ToCampaignDto(await _inner.GetClubCampaignAsync(clubId, campaignId, c)),
            ToolboxCacheContext.Default.CacheEntryCampaignDto, ct);

    public async Task<ITrackOfTheDayCollection> GetTrackOfTheDaysAsync(int monthOffset, CancellationToken ct = default) =>
        await GetCachedAsync($"totd_offset_{monthOffset}", _config.DynamicExpirationMinutes,
            async (c) => ToTrackOfTheDayCollectionDto(await _inner.GetTrackOfTheDaysAsync(monthOffset, c)),
            ToolboxCacheContext.Default.CacheEntryTrackOfTheDayCollectionDto, ct);

    public async Task<ILeaderboard> GetLeaderboardAsync(string mapUid, string accountId, CancellationToken ct = default) =>
        await GetCachedAsync($"leaderboard_{mapUid}_{accountId}", _config.HighlyDynamicExpirationMinutes,
            async (c) => ToLeaderboardDto(await _inner.GetLeaderboardAsync(mapUid, accountId, c)),
            ToolboxCacheContext.Default.CacheEntryLeaderboardDto, ct);

    public async Task<ITmxMap?> GetTmxMapAsync(int id, CancellationToken ct = default) =>
        await GetCachedAsync($"tmx_map_{id}", _config.StaticExpirationMinutes,
            async (c) => ToTmxMapDto(await _inner.GetTmxMapAsync(id, c)),
            ToolboxCacheContext.Default.CacheEntryTmxMapDto, ct);

    public string GetTmxMapUrl(int id) => _inner.GetTmxMapUrl(id);

    public async Task<IEnumerable<ITmxMap>> SearchTmxMapsAsync(string? name, string? author, string sort, bool desc, CancellationToken ct = default)
    {
        var list = await GetCachedAsync($"tmx_search_{name}_{author}_{sort}_{desc}", _config.DynamicExpirationMinutes,
            async (c) => (await _inner.SearchTmxMapsAsync(name, author, sort, desc, c)).Select(ToTmxMapDto).ToList()!,
            ToolboxCacheContext.Default.CacheEntryListTmxMapDto, ct);
        return list.Cast<ITmxMap>();
    }

    public Task<ITmxMap?> GetRandomTmxMapAsync(CancellationToken ct = default) => _inner.GetRandomTmxMapAsync(ct);

    public async Task<ITmxMapPack?> GetTmxMapPackAsync(int id, CancellationToken ct = default) =>
        await GetCachedAsync($"tmx_pack_{id}", _config.StaticExpirationMinutes,
            async (c) => ToTmxMapPackDto(await _inner.GetTmxMapPackAsync(id, c)),
            ToolboxCacheContext.Default.CacheEntryTmxMapPackDto, ct);

    public async Task<IEnumerable<ITmxMap>> GetTmxMapPackMapsAsync(int id, CancellationToken ct = default)
    {
        var list = await GetCachedAsync($"tmx_pack_maps_{id}", _config.StaticExpirationMinutes,
            async (c) => (await _inner.GetTmxMapPackMapsAsync(id, c)).Select(ToTmxMapDto).ToList(),
            ToolboxCacheContext.Default.CacheEntryListTmxMapDto, ct);
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
