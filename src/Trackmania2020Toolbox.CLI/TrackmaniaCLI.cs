using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GBX.NET;
using GBX.NET.LZO;

namespace Trackmania2020Toolbox;

public class RealConsole : IConsole
{
    public void WriteLine(string? value = null) => Console.WriteLine(value);
    public void Write(string? value = null) => Console.Write(value);
    public string? ReadLine() => Console.ReadLine();
    public Task<int> SelectItemAsync(string title, IEnumerable<string> items)
    {
        var itemList = items.ToList();
        WriteLine($"\n{title}:");
        for (int i = 0; i < itemList.Count; i++)
        {
            WriteLine($"{i + 1}: {itemList[i]}");
        }

        while (true)
        {
            Write("\nSelect a number (or 0 to cancel): ");
            var input = ReadLine();
            if (int.TryParse(input, out var choice))
            {
                if (choice >= 0 && choice <= itemList.Count)
                {
                    return Task.FromResult(choice);
                }
                WriteLine($"Invalid choice: {choice}. Please select a number between 0 and {itemList.Count}.");
            }
            else
            {
                WriteLine($"Invalid input: '{input}'. Please enter a number.");
            }
        }
    }
}

public static class TrackmaniaCLI
{
    public static readonly string UserAgent = "Trackmania2020Toolbox/1.0 (+https://github.com/AI-Citizen/Trackmania2020Toolbox)";
    public static readonly HttpClient HttpClient = new HttpClient();

    public static string GetScriptDirectory() => AppDomain.CurrentDomain.BaseDirectory;

    public static async Task Run(string[] args)
    {
        Gbx.LZO = new Lzo();
        if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return;
        }

        var fs = new RealFileSystem();
        var console = new RealConsole();
        var configService = new RealConfigService(fs, console);
        var scriptDir = GetScriptDirectory();
        var baseConfig = await configService.LoadConfigAsync(scriptDir);
        var config = ParseArguments(args, baseConfig);

        using var rawApi = new TrackmaniaApiWrapper(HttpClient, UserAgent);
        using var api = new CachedTrackmaniaApi(rawApi, fs, scriptDir, config.Cache);
        var net = new RealNetworkService(HttpClient);
        var fixer = new RealMapFixer();
        var dateTime = new RealDateTime();

        var app = new ToolboxApp(api, fs, net, fixer, console, dateTime, scriptDir, configService);
        await app.RunAsync(config);
    }

    public static Config ParseArguments(string[] args, Config baseConfig)
    {
        var config = baseConfig;
        var dl = config.Downloader;
        var tmx = config.Tmx;
        var fixer = config.Fixer;
        var app = config.App;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--weekly-shorts":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) dl.WeeklyShorts = args[++i];
                    else dl.WeeklyShorts = "latest";
                    break;
                case "--weekly-grands":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) dl.WeeklyGrands = args[++i];
                    else dl.WeeklyGrands = "latest";
                    break;
                case "--seasonal":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) dl.Seasonal = args[++i];
                    else dl.Seasonal = "latest";
                    break;
                case "--club-campaign":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) dl.ClubCampaign = args[++i];
                    break;
                case "--totd":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) dl.ToTDDate = args[++i];
                    else dl.ToTDDate = "latest";
                    break;
                case "--export-campaign-medals":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        dl.ExportMedalsPlayerId = args[++i];
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        {
                            dl.ExportMedalsCampaign = args[++i];
                        }
                    }
                    break;
                case "--tmx":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) tmx.TmxMaps = args[++i];
                    break;
                case "--tmx-pack":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) tmx.TmxPacks = args[++i];
                    break;
                case "--tmx-search":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) tmx.TmxSearch = args[++i];
                    break;
                case "--tmx-author":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) tmx.TmxAuthor = args[++i];
                    break;
                case "--tmx-sort":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) tmx.TmxSort = args[++i];
                    break;
                case "--tmx-desc":
                    tmx.TmxDesc = true;
                    break;
                case "--tmx-random":
                    tmx.TmxRandom = true;
                    break;
                case "--folder":
                case "-f":
                    if (i + 1 < args.Length)
                    {
                        fixer.FolderPath = args[++i];
                        fixer.ExplicitFolder = true;
                    }
                    break;
                case "--skip-title-update":
                    fixer.UpdateTitle = false;
                    break;
                case "--skip-maptype-convert":
                    fixer.ConvertPlatformMapType = false;
                    break;
                case "--dry-run":
                    fixer.DryRun = true;
                    break;
                case "--force":
                    app.ForceOverwrite = true;
                    break;
                case "--non-interactive":
                    app.Interactive = false;
                    break;
                case "--play":
                    app.Play = true;
                    break;
                case "--set-game-path":
                    if (i + 1 < args.Length) app.SetGamePath = args[++i];
                    break;
                case "--download-delay":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var delay)) dl.DownloadDelayMs = delay;
                    break;
                default:
                    if (!args[i].StartsWith("--"))
                    {
                        app.ExtraPaths.Add(args[i]);
                    }
                    break;
            }
        }

        return config;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Trackmania 2020 Toolbox");
        Console.WriteLine("Usage: dotnet run --project src/Trackmania2020Toolbox.CLI/Trackmania2020Toolbox.CLI.csproj -- [options] [maps/folders...]");
        Console.WriteLine("\nDownload Options:");
        Console.WriteLine("  --weekly-shorts [weeks]    Download Weekly Shorts (e.g., \"68, 70-72\"). Defaults to latest.");
        Console.WriteLine("  --weekly-grands [weeks]    Download Weekly Grands (e.g., \"65\"). Defaults to latest.");
        Console.WriteLine("  --seasonal [name]          Download Seasonal Campaign (e.g., \"Winter 2024\"). Defaults to latest.");
        Console.WriteLine("  --club-campaign <search|id> Download Club Campaign. Searches by name if not <clubId>/<campId>.");
        Console.WriteLine("  --totd [date]              Download Track of the Day. Defaults to today.");
        Console.WriteLine("                             Formats: YYYY-MM, YYYY-MM-DD, YYYY-MM-DD-DD (range)");
        Console.WriteLine("  --tmx <ids|urls>           Download maps from Trackmania Exchange (comma-separated).");
        Console.WriteLine("  --tmx-pack <ids|urls>      Download map packs from Trackmania Exchange.");
        Console.WriteLine("  --tmx-search <name>        Search for maps on TMX.");
        Console.WriteLine("  --tmx-author <name>        Search for maps by author on TMX.");
        Console.WriteLine("  --tmx-sort <sort>          Sort search results (name, author, awards, downloads). Default: name.");
        Console.WriteLine("  --tmx-desc                 Sort search results in descending order.");
        Console.WriteLine("  --tmx-random               Download a random map from TMX.");
        Console.WriteLine("  --download-delay <ms>      Delay between API requests (default: 1000).");
        Console.WriteLine("  --export-campaign-medals <PlayerID> [campaign]");
        Console.WriteLine("                             Export official campaign medals to medals.csv.");
        // We could use constants here, but they are not easily available yet.
        Console.WriteLine("\nPlay Options:");
        Console.WriteLine("  --play                     Launch Trackmania with the maps (requires game running)");
        Console.WriteLine("  --set-game-path <path>     Set the path to Trackmania.exe in config.toml");
        Console.WriteLine("\nOther Options:");
        Console.WriteLine("  --force                    Overwrite existing files");
        Console.WriteLine("  --non-interactive          Disable interactive mode (don't ask for selection)");
        Console.WriteLine("  --folder, -f <path>        Folder for batch fixing (default: Documents\\Trackmania2020\\Maps\\Toolbox)");
        Console.WriteLine($"  --skip-title-update        Do not update TitleId ({RealMapFixer.LegacyTitleId} -> {RealMapFixer.TargetTitleId})");
        Console.WriteLine($"  --skip-maptype-convert     Do not convert MapType ({RealMapFixer.LegacyMapType.Replace("TrackMania\\", "")} -> {RealMapFixer.TargetMapType.Replace("TrackMania\\", "")})");
        Console.WriteLine("  --dry-run                  Show changes without saving");
        Console.WriteLine("  --help, -h                 Show this help message");
        Console.WriteLine("\nPositional arguments:");
        Console.WriteLine("  [maps/folders...]          Individual maps or folders to process and/or play");
    }
}
