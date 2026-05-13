using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using Tomlyn;
using GBX.NET;
using GBX.NET.Engines.Game;
using TmEssentials;

namespace Trackmania2020Toolbox;

public class RealConfigService(IFileSystem fs, IConsole? console = null) : IConfigService, IDisposable
{
    private readonly IFileSystem _fs = fs;
    private readonly IConsole? _console = console;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Config> LoadConfigAsync(string scriptDirectory, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var configPath = Path.Combine(scriptDirectory, "config.toml");
            if (!_fs.FileExists(configPath)) return Config.Default;

            var content = await _fs.ReadAllTextAsync(configPath, ct);
            return LoadConfigInternal(configPath, content);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Config LoadConfig(string scriptDirectory)
    {
        _semaphore.Wait();
        try
        {
            var configPath = Path.Combine(scriptDirectory, "config.toml");
            if (!_fs.FileExists(configPath)) return Config.Default;

            var content = _fs.ReadAllText(configPath);
            return LoadConfigInternal(configPath, content);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private Config LoadConfigInternal(string configPath, string content)
    {
        try
        {
            var config = TomlSerializer.Deserialize<Config>(content, ToolboxConfigContext.Default.Config);
            if (config != null) return config;
        }
        catch (Exception ex)
        {
            Log($"[ConfigService] ERROR: Failed to load configuration from '{configPath}'. The file might be corrupted or inaccessible. Falling back to default settings.");
            Log($"[ConfigService] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Log($"[ConfigService] INNER EXCEPTION: {ex.InnerException.Message}");
            }
        }

        return Config.Default;
    }

    public async Task SaveConfigAsync(string scriptDirectory, Config config, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var configPath = Path.Combine(scriptDirectory, "config.toml");
            var content = TomlSerializer.Serialize(config, ToolboxConfigContext.Default.Config);
            await _fs.WriteAllTextAsync(configPath, content, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void Log(string message)
    {
        if (_console != null) _console.WriteLine(message);
        else Console.WriteLine(message);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class RealBrowserService(IFileSystem fs) : IBrowserService
{
    private readonly IFileSystem _fs = fs;

    public IEnumerable<BrowserItem> GetBrowserItems(string directory, string filter, bool descending)
    {
        if (!_fs.DirectoryExists(directory)) return [];

        var filteredDirs = _fs.GetDirectories(directory)
            .Select(Path.GetFileName)
            .Where(name => string.IsNullOrEmpty(filter) || name!.Contains(filter, StringComparison.OrdinalIgnoreCase));

        var sortedDirs = descending ? filteredDirs.OrderByDescending(d => d) : filteredDirs.OrderBy(d => d);

        var dirItems = sortedDirs
            .Select(name => new BrowserItem(name!, Path.Combine(directory, name!), true));

        var filteredFiles = _fs.GetFiles(directory, "*.Map.Gbx", SearchOption.TopDirectoryOnly)
            .Select(f =>
            {
                var fn = Path.GetFileName(f);
                return (Path: f, FileName: fn, DisplayName: TextFormatter.Deformat(fn));
            })
            .Where(f => string.IsNullOrEmpty(filter) || f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) || f.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));

        var sortedFiles = descending
            ? filteredFiles.OrderByDescending(f => f.DisplayName)
            : filteredFiles.OrderBy(f => f.DisplayName);

        var fileItems = sortedFiles.Select(f => new BrowserItem(f.DisplayName, f.Path, false));

        return dirItems.Concat(fileItems).ToList();
    }
}

public class RealFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool FileExists(string path) => File.Exists(path);
    public void DeleteFile(string path) => File.Delete(path);
    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct = default) => File.WriteAllBytesAsync(path, bytes, ct);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default) => File.WriteAllTextAsync(path, contents, ct);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => File.ReadAllTextAsync(path, ct);
    public string[] ReadAllLines(string path) => File.ReadAllLines(path);
    public void WriteAllLines(string path, IEnumerable<string> contents) => File.WriteAllLines(path, contents);
    public Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken ct = default) => File.WriteAllLinesAsync(path, contents, ct);
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);
    public string[] GetDirectories(string path) => Directory.GetDirectories(path);
}

public class RealNetworkService(HttpClient httpClient) : INetworkService
{
    private readonly HttpClient _httpClient = httpClient;
    public Task<byte[]> GetByteArrayAsync(string url, CancellationToken ct = default) => _httpClient.GetByteArrayAsync(url, ct);
}

public class RealMapFixer : IMapFixer
{
    public const string LegacyTitleId = "OrbitalDev@falguiere";
    public const string TargetTitleId = "TMStadium";
    public const string LegacyMapType = "TrackMania\\TM_Platform";
    public const string TargetMapType = "TrackMania\\TM_Race";

    public async Task<bool> ProcessFileAsync(string filePath, Config cfg, CancellationToken ct = default)
    {
        var fixerCfg = cfg.Fixer;
        if (!fixerCfg.UpdateTitle && !fixerCfg.ConvertPlatformMapType) return false;

        var gbx = await Gbx.ParseAsync<CGameCtnChallenge>(filePath, cancellationToken: ct);
        if (gbx?.Node == null) return false;

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
