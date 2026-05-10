using System.Diagnostics;
using System.Net.Http;
using ManiaAPI.TrackmaniaIO;
using ManiaAPI.TMX;
using TmEssentials;

namespace Trackmania2020Toolbox;

public class TrackmaniaApiWrapper(HttpClient httpClient, string userAgent) : ITrackmaniaApi
{
    private readonly TrackmaniaIO _api = new(httpClient, userAgent);
    private readonly MX _tmx = new(httpClient, TmxSite.Trackmania);
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private long _lastRequestTimestamp;

    public int DelayMs { get; set; }

    internal async Task ApplyDelayAsync()
    {
        if (DelayMs <= 0) return;

        await _semaphore.WaitAsync();
        try
        {
            long now = Stopwatch.GetTimestamp();
            long elapsedTicks = Stopwatch.GetElapsedTime(_lastRequestTimestamp, now).Ticks;
            long minIntervalTicks = DelayMs * TimeSpan.TicksPerMillisecond;

            if (elapsedTicks < minIntervalTicks)
            {
                int delay = (int)((minIntervalTicks - elapsedTicks) / TimeSpan.TicksPerMillisecond);
                if (delay > 0) await Task.Delay(delay);
            }

            _lastRequestTimestamp = Stopwatch.GetTimestamp();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<ICampaignCollection> GetWeeklyShortCampaignsAsync(int page)
    {
        await ApplyDelayAsync();
        return new CampaignCollectionProxy(await _api.GetWeeklyShortCampaignsAsync(page));
    }

    public async Task<ICampaign> GetWeeklyShortCampaignAsync(int id)
    {
        await ApplyDelayAsync();
        return new CampaignProxy(await _api.GetWeeklyShortCampaignAsync(id));
    }

    public async Task<ICampaignCollection> GetWeeklyGrandCampaignsAsync(int page)
    {
        await ApplyDelayAsync();
        return new CampaignCollectionProxy(await _api.GetWeeklyGrandCampaignsAsync(page));
    }

    public async Task<ICampaign> GetWeeklyGrandCampaignAsync(int id)
    {
        await ApplyDelayAsync();
        return new CampaignProxy(await _api.GetWeeklyGrandCampaignAsync(id));
    }

    public async Task<ICampaignCollection> GetSeasonalCampaignsAsync(int page)
    {
        await ApplyDelayAsync();
        return new CampaignCollectionProxy(await _api.GetSeasonalCampaignsAsync(page));
    }

    public async Task<ICampaign> GetSeasonalCampaignAsync(int id)
    {
        await ApplyDelayAsync();
        return new CampaignProxy(await _api.GetSeasonalCampaignAsync(id));
    }

    public async Task<ICampaignCollection> GetClubCampaignsAsync(int page)
    {
        await ApplyDelayAsync();
        return new CampaignCollectionProxy(await _api.GetClubCampaignsAsync(page));
    }

    public async Task<ICampaign> GetClubCampaignAsync(int clubId, int campaignId)
    {
        await ApplyDelayAsync();
        return new CampaignProxy(await _api.GetClubCampaignAsync(clubId, campaignId));
    }

    public async Task<ITrackOfTheDayCollection> GetTrackOfTheDaysAsync(int monthOffset)
    {
        await ApplyDelayAsync();
        var result = await _api.GetTrackOfTheDaysAsync(monthOffset);
        return new TrackOfTheDayCollectionProxy(result);
    }

    public async Task<ILeaderboard> GetLeaderboardAsync(string mapUid, string accountId)
    {
        await ApplyDelayAsync();
        var result = await _api.GetLeaderboardAsync(mapUid, accountId);
        return new LeaderboardProxy(result);
    }

    public async Task<ITmxMap?> GetTmxMapAsync(int id)
    {
        await ApplyDelayAsync();
        var result = await _tmx.SearchMapsAsync(new MX.SearchMapsParameters { Id = [id] });
        return result.Results.Count > 0 ? new TmxMapProxy(result.Results[0]) : null;
    }

    public string GetTmxMapUrl(int id) => _tmx.GetMapGbxUrl(id);

    public async Task<IEnumerable<ITmxMap>> SearchTmxMapsAsync(string? name, string? author, string sort, bool desc)
    {
        await ApplyDelayAsync();
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
        await ApplyDelayAsync();
        var result = await _tmx.SearchMapsAsync(new MX.SearchMapsParameters { Random = 1, Count = 1 });
        return result.Results.Count > 0 ? new TmxMapProxy(result.Results[0]) : null;
    }

    public async Task<ITmxMapPack?> GetTmxMapPackAsync(int id)
    {
        await ApplyDelayAsync();
        var result = await _tmx.SearchMappacksAsync(new MX.SearchMappacksParameters { Id = [id] });
        return result.Results.Count > 0 ? new TmxMapPackProxy(result.Results[0]) : null;
    }

    public async Task<IEnumerable<ITmxMap>> GetTmxMapPackMapsAsync(int id)
    {
        await ApplyDelayAsync();
        var result = await _tmx.SearchMapsAsync(new MX.SearchMapsParameters { MappackId = id });
        return result.Results.Select(r => new TmxMapProxy(r));
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    private class TmxMapProxy(MapItem obj) : ITmxMap
    {
        private readonly MapItem _obj = obj;
        public int Id => (int)_obj.MapId;
        public string Name => _obj.Name;
        public string AuthorName => _obj.Uploader.Name;
        public int AwardCount => _obj.AwardCount;
        public int DownloadCount => _obj.DownloadCount;
    }

    private class TmxMapPackProxy(MappackItem obj) : ITmxMapPack
    {
        private readonly MappackItem _obj = obj;
        public int Id => (int)_obj.MappackId;
        public string Name => _obj.Name;
    }

    private class CampaignCollectionProxy(CampaignCollection obj) : ICampaignCollection
    {
        private readonly CampaignCollection _obj = obj;
        public IEnumerable<ICampaignItem> Campaigns => _obj.Campaigns.Select(c => new CampaignItemProxy(c));
        public int PageCount => _obj.PageCount;
    }

    private class CampaignItemProxy(CampaignItem obj) : ICampaignItem
    {
        private readonly CampaignItem _obj = obj;
        public int Id => _obj.Id;
        public string Name => _obj.Name;
        public int? ClubId => _obj.ClubId;
    }

    private class CampaignProxy(Campaign obj) : ICampaign
    {
        private readonly Campaign _obj = obj;
        public IEnumerable<IMap> Playlist => _obj.Playlist.Select(m => new MapProxy(m));
        public string Name => _obj.Name;
        public string? ClubName => _obj.ClubName;
    }

    private class MapProxy(Map obj) : IMap
    {
        private readonly Map _obj = obj;
        public string Name => _obj.Name;
        public string? FileName => _obj.FileName;
        public string? FileUrl => _obj.FileUrl;
        public string MapUid => _obj.MapUid;
        public TimeInt32 AuthorScore => _obj.AuthorScore;
        public TimeInt32 GoldScore => _obj.GoldScore;
        public TimeInt32 SilverScore => _obj.SilverScore;
        public TimeInt32 BronzeScore => _obj.BronzeScore;
    }

    private class TrackOfTheDayCollectionProxy(TrackOfTheDayMonth obj) : ITrackOfTheDayCollection
    {
        private readonly TrackOfTheDayMonth _obj = obj;
        public int Year => _obj.Year;
        public int Month => _obj.Month;
        public IEnumerable<ITrackOfTheDayDay> Days => _obj.Days.Select(d => new TrackOfTheDayDayProxy(d));
    }

    private class TrackOfTheDayDayProxy(TrackOfTheDay obj) : ITrackOfTheDayDay
    {
        private readonly TrackOfTheDay _obj = obj;
        public int MonthDay => _obj.MonthDay;
        public ITrackmaniaMap? Map => _obj.Map != null ? new TrackmaniaMapProxy(_obj.Map) : null;
    }

    private class TrackmaniaMapProxy(Map obj) : ITrackmaniaMap
    {
        private readonly Map _obj = obj;
        public string Name => _obj.Name;
        public string? FileName => _obj.FileName;
        public string? FileUrl => _obj.FileUrl;
    }

    private class LeaderboardProxy(Leaderboard obj) : ILeaderboard
    {
        private readonly Leaderboard _obj = obj;
        public IEnumerable<IRecord> Tops => _obj.Tops.Select(r => new RecordProxy(r));
    }

    private class RecordProxy(Record obj) : IRecord
    {
        private readonly Record _obj = obj;
        public TimeInt32 Time => _obj.Time;
    }
}
