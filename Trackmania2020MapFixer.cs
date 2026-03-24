#:package GBX.NET@2.*
#:package GBX.NET.LZO@2.*

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;

internal static class MapFixer
{
    private static readonly string DefaultFolder = DetermineDefaultFolder();
    private const string FilePattern = "*.Map.Gbx";

    public static void Main(string[] args)
    {
        Gbx.LZO = new Lzo();

        var config = ParseArguments(args);

        Console.WriteLine($"Using folder: {config.FolderPath}");


        if (!config.UpdateTitle && !config.ConvertPlatformMapType)
        {
            Console.WriteLine("No action flag specified, showing help:");
            PrintUsage();
            return;
        }

        if (!Directory.Exists(config.FolderPath))
        {
            Console.WriteLine($"The specified folder does not exist: {config.FolderPath}");
            return;
        }

        var files = GetMapFiles(config.FolderPath).ToList();

        var filesAnalyzed = 0;
        var filesChanged = 0;

        foreach (var file in files)
        {
            try
            {
                if (ProcessFile(file, config))
                {
                    filesChanged++;
                }

                filesAnalyzed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process {file}: {ex.Message}");
            }
        }

        Console.WriteLine("\nAnalysis complete.");
        Console.WriteLine($"Files analyzed successfully: {filesAnalyzed} out of {files.Count}");
        Console.WriteLine($"Files updated: {filesChanged}");
    }

    private static Config ParseArguments(string[] arguments)
    {
        var folder = DefaultFolder;
        var updateTitle = false;
        var convertPlatformMapType = false;
        var dryRun = false;

        for (var i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];
            switch (arg.ToLowerInvariant())
            {
                case "--folder":
                case "-f":
                    if (i + 1 < arguments.Length && !string.IsNullOrWhiteSpace(arguments[i + 1]))
                    {
                        folder = arguments[++i];
                    }
                    break;
                case "--update-title":
                    updateTitle = true;
                    break;
                case "--convert-platform-maptype":
                    convertPlatformMapType = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    if (!arg.StartsWith("-"))
                    {
                        folder = arg;
                    }
                    break;
            }
        }

        return new Config(folder, updateTitle, convertPlatformMapType, dryRun);
    }

    private static IEnumerable<string> GetMapFiles(string folderPath)
    {
        return Directory.GetFiles(folderPath, FilePattern, SearchOption.AllDirectories);
    }

    private static bool ProcessFile(string filePath, Config cfg)
    {
        var gbx = Gbx.Parse<CGameCtnChallenge>(filePath);
        var map = gbx.Node;

        if (!ApplyFixes(map, cfg))
        {
            return false;
        }

        if (cfg.DryRun)
        {
            Console.WriteLine($"Dry run: would save {filePath}");
        }
        else
        {
            gbx.Save(filePath);
            Console.WriteLine($"Saved: {filePath}");
        }

        return true;
    }

    private static bool ApplyFixes(CGameCtnChallenge map, Config cfg)
    {
        var changed = false;

        if (cfg.UpdateTitle && map.TitleId == "OrbitalDev@falguiere")
        {
            map.TitleId = "TMStadium";
            changed = true;
        }

        if (cfg.ConvertPlatformMapType && map.MapType == "TrackMania\\TM_Platform")
        {
            map.MapType = "TrackMania\\TM_Race";
            changed = true;
        }

        return changed;
    }

    private static string DetermineDefaultFolder()
    {
        var cwd = Environment.CurrentDirectory;
        if (Directory.Exists(cwd))
        {
            return cwd;
        }

        var exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return exeDir;
    }

    private record Config(string FolderPath, bool UpdateTitle, bool ConvertPlatformMapType, bool DryRun);

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Trackmania2020MapFixer [folder] [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --folder, -f <path>       Folder containing .Map.Gbx files (default: current exe directory)");
        Console.WriteLine("  --update-title            Enable title ID migration from OrbitalDev@falguiere to TMStadium");
        Console.WriteLine("  --convert-platform-maptype Enable map type migration from TrackMania\\TM_Platform to TrackMania\\TM_Race");
        Console.WriteLine("  --dry-run                 Show files that would be changed without saving");
        Console.WriteLine("  --help, -h                Show this help message");
    }
}
