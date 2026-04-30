using System.Buffers;
using TmEssentials;

namespace Trackmania2020Toolbox;

public class MapDownloader(IFileSystem fs, INetworkService net, IMapFixer fixer, IConsole console)
{
    private readonly IFileSystem _fs = fs;
    private readonly INetworkService _net = net;
    private readonly IMapFixer _fixer = fixer;
    private readonly IConsole _console = console;

    private static readonly SearchValues<char> InvalidFileNameChars = SearchValues.Create(
        Path.GetInvalidFileNameChars()
        .Union(['/', '\\', ':', '*', '?', '\"', '<', '>', '|'])
        .Distinct()
        .ToArray());

    public async Task<List<string>> DownloadAndFixMapsAsync(IEnumerable<MapDownloadRecord> maps, string downloadDir, Config config)
    {
        if (!_fs.DirectoryExists(downloadDir)) _fs.CreateDirectory(downloadDir);

        var processedPaths = new List<string>();
        var mapList = maps.ToList();
        _console.WriteLine($"Processing {mapList.Count} maps...");

        for (int i = 0; i < mapList.Count; i++)
        {
            var map = mapList[i];
            if (string.IsNullOrEmpty(map.FileUrl)) continue;

            var fileName = (map.FileName ?? map.Name).Trim();
            var deformattedName = TextFormatter.Deformat(map.Name);
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

            fileName = SanitizeString(fileName);

            if (!string.IsNullOrEmpty(map.Prefix)) fileName = map.Prefix + fileName;

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
                await Task.Delay(config.Downloader.DownloadDelayMs);
            }
            catch (Exception ex)
            {
                _console.WriteLine($"\n  Failed: {ex.Message}");
            }
        }
        return processedPaths;
    }

    private static string SanitizeString(string input)
    {
        return string.Create(input.Length, input, (span, state) =>
        {
            state.AsSpan().CopyTo(span);
            int index;
            while ((index = span.IndexOfAny(InvalidFileNameChars)) != -1)
            {
                span[index] = '_';
            }
        });
    }
}
