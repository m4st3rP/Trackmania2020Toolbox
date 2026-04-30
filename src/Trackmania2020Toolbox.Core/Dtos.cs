using System.Text.Json.Serialization;
using TmEssentials;

namespace Trackmania2020Toolbox;

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

public class CacheEntry<T>
{
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; }
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
