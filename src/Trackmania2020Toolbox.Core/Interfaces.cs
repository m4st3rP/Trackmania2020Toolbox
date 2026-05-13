using System.Threading;
using TmEssentials;

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
    int DelayMs { get; set; }
    Task<ICampaignCollection> GetWeeklyShortCampaignsAsync(int page, CancellationToken ct = default);
    Task<ICampaign> GetWeeklyShortCampaignAsync(int id, CancellationToken ct = default);
    Task<ICampaignCollection> GetWeeklyGrandCampaignsAsync(int page, CancellationToken ct = default);
    Task<ICampaign> GetWeeklyGrandCampaignAsync(int id, CancellationToken ct = default);
    Task<ICampaignCollection> GetSeasonalCampaignsAsync(int page, CancellationToken ct = default);
    Task<ICampaign> GetSeasonalCampaignAsync(int id, CancellationToken ct = default);
    Task<ICampaignCollection> GetClubCampaignsAsync(int page, CancellationToken ct = default);
    Task<ICampaign> GetClubCampaignAsync(int clubId, int campaignId, CancellationToken ct = default);
    Task<ITrackOfTheDayCollection> GetTrackOfTheDaysAsync(int monthOffset, CancellationToken ct = default);
    Task<ILeaderboard> GetLeaderboardAsync(string mapUid, string accountId, CancellationToken ct = default);

    Task<ITmxMap?> GetTmxMapAsync(int id, CancellationToken ct = default);
    string GetTmxMapUrl(int id);
    Task<IEnumerable<ITmxMap>> SearchTmxMapsAsync(string? name, string? author, string sort, bool desc, CancellationToken ct = default);
    Task<ITmxMap?> GetRandomTmxMapAsync(CancellationToken ct = default);
    Task<ITmxMapPack?> GetTmxMapPackAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<ITmxMap>> GetTmxMapPackMapsAsync(int id, CancellationToken ct = default);
}

public interface IFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    bool FileExists(string path);
    void DeleteFile(string path);
    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct = default);
    void WriteAllText(string path, string contents);
    Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default);
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
    string[] ReadAllLines(string path);
    void WriteAllLines(string path, IEnumerable<string> contents);
    Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken ct = default);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    string[] GetDirectories(string path);
}

public interface INetworkService
{
    Task<byte[]> GetByteArrayAsync(string url, CancellationToken ct = default);
}

public interface IMapFixer
{
    Task<bool> ProcessFileAsync(string filePath, Config config, CancellationToken ct = default);
}

public interface IDateTime { DateTime UtcNow { get; } }

public interface IConsole
{
    void WriteLine(string? value = null);
    void Write(string? value = null);
    string? ReadLine();
    Task<int> SelectItemAsync(string title, IEnumerable<string> items);
}

public interface IBrowserService
{
    IEnumerable<BrowserItem> GetBrowserItems(string directory, string filter, bool descending);
}

public interface IInputParser
{
    int ParseWeeklyShortsNum(string name);
    int ParseWeeklyGrandsNum(string name);
    List<int> ParseNumbers(string input);
    List<(MapRef Start, MapRef End)> ParseMapRanges(string input);
    MapRef? ParseMapRef(string s);
    List<(SeasonalRef Start, SeasonalRef End)> ParseSeasonalRanges(string input);
    SeasonalRef? ParseSeasonalRef(string s);
    SeasonalRef? ParseSeasonalRefFromCampaignName(string campaignName);
    List<(DateTime Start, DateTime End)> ParseToTdRanges(string input, DateTime now);
    List<int> ParseTmxIds(string input);
    string FormatSeasonalFolderName(string campaignName);
}

public interface IMapDownloader
{
    Task<List<string>> DownloadAndFixMapsAsync(IEnumerable<MapDownloadRecord> maps, string downloadDir, Config config, CancellationToken ct = default);
}

public interface IConfigService
{
    Task<Config> LoadConfigAsync(string scriptDirectory, CancellationToken ct = default);
    Task SaveConfigAsync(string scriptDirectory, Config config, CancellationToken ct = default);
    Config LoadConfig(string scriptDirectory);
}
