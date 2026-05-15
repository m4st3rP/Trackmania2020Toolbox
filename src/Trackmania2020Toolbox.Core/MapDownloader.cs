using System.Buffers;
using TmEssentials;

namespace Trackmania2020Toolbox;

public class MapDownloader(IFileSystem fs, INetworkService net, IMapFixer fixer, IConsole console) : IMapDownloader
{
    private readonly IFileSystem _fs = fs;
    private readonly INetworkService _net = net;
    private readonly IMapFixer _fixer = fixer;
    private readonly IConsole _console = console;

    public async Task<List<string>> DownloadAndFixMapsAsync(IEnumerable<MapDownloadRecord> maps, string downloadDir, Config config)
    {
        if (!_fs.DirectoryExists(downloadDir)) _fs.CreateDirectory(downloadDir);

        List<string> processedPaths = [];
        var mapList = maps.ToList();
        _console.WriteLine($"Processing {mapList.Count} maps...");

        for (int i = 0; i < mapList.Count; i++)
        {
            if (i > 0 && config.Downloader.DownloadDelayMs > 0)
            {
                await Task.Delay(config.Downloader.DownloadDelayMs);
            }

            var map = mapList[i];
            if (string.IsNullOrEmpty(map.FileUrl)) continue;

            var deformattedName = TextFormatter.Deformat(map.Name);
            var rawFileName = (map.FileName ?? map.Name).AsSpan().Trim();
            var fileNameStr = TextFormatter.Deformat(rawFileName.ToString());

            string extension = ".Map.Gbx";
            if (fileNameStr.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase))
            {
                fileNameStr = fileNameStr[..^8].Trim();
            }
            else if (fileNameStr.EndsWith(".Gbx", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".Gbx";
                fileNameStr = fileNameStr[..^4].Trim();
            }

            fileNameStr = PathUtilities.SanitizeString(fileNameStr + extension);

            if (!string.IsNullOrEmpty(map.Prefix)) fileNameStr = map.Prefix + fileNameStr;

            var filePath = Path.Combine(downloadDir, fileNameStr);
            _console.Write($"[{i + 1}/{mapList.Count}] {deformattedName}... ");

            if (_fs.FileExists(filePath) && !config.App.ForceOverwrite)
            {
                _console.WriteLine("Skipped (already exists)");
                processedPaths.Add(filePath);
                continue;
            }

            try
            {
                var fileData = await _net.GetByteArrayAsync(map.FileUrl);
                await _fs.WriteAllBytesAsync(filePath, fileData);
                _console.Write("Downloaded and ");

                if (await _fixer.ProcessFileAsync(filePath, config))
                {
                    var fileNameOnly = Path.GetFileName(filePath);
                    var deformattedFileName = TextFormatter.Deformat(fileNameOnly);
                    if (config.Fixer.DryRun) _console.WriteLine($"  [Dry Run] Would update: {deformattedFileName}");
                    else _console.WriteLine("Fixed.");
                }
                else _console.WriteLine("Saved.");

                processedPaths.Add(filePath);
            }
            catch (Exception ex)
            {
                _console.WriteLine($"\n  Failed: {ex.Message}");
            }
        }
        return processedPaths;
    }
}
