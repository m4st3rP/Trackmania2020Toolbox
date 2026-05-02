using Tomlyn.Serialization;

namespace Trackmania2020Toolbox;

public class DownloaderConfig
{
    public string? WeeklyShorts { get; set; }
    public string? WeeklyGrands { get; set; }
    public string? Seasonal { get; set; }
    public string? ClubCampaign { get; set; }
    public string? ToTDDate { get; set; }
    public string? ExportMedalsPlayerId { get; set; }
    public string? ExportMedalsCampaign { get; set; }
    public string? ExportMedalsOutputPath { get; set; }
    public int DownloadDelayMs { get; set; } = 1000;
}

public class TmxConfig
{
    public string? TmxMaps { get; set; }
    public string? TmxPacks { get; set; }
    public string? TmxSearch { get; set; }
    public string? TmxAuthor { get; set; }
    public string TmxSort { get; set; } = "name";
    public bool TmxDesc { get; set; }
    public bool TmxRandom { get; set; }
}

public class FixerConfig
{
    public string FolderPath { get; set; } = "";
    public bool ExplicitFolder { get; set; }
    public bool UpdateTitle { get; set; } = true;
    public bool ConvertPlatformMapType { get; set; } = true;
    public bool DryRun { get; set; }
}

public class AppConfig
{
    public bool ForceOverwrite { get; set; }
    public bool Interactive { get; set; } = true;
    public bool Play { get; set; }
    public string? SetGamePath { get; set; }
    public List<string> ExtraPaths { get; set; } = new();
}

public class DesktopConfig
{
    public string BrowserFolder { get; set; } = "";
    public bool DoubleClickToPlay { get; set; } = true;
    public bool EnterToPlay { get; set; } = true;
    public bool PlayAfterDownload { get; set; }
    public bool SaveLastFolder { get; set; }
    public string LastFolder { get; set; } = "";
    public bool SaveLastSort { get; set; }
    public int LastSort { get; set; }
}

public class CacheConfig
{
    public bool Enabled { get; set; } = true;
    public int StaticExpirationMinutes { get; set; } = 43200; // 30 days
    public int DynamicExpirationMinutes { get; set; } = 60; // 1 hour
    public int HighlyDynamicExpirationMinutes { get; set; } = 5; // 5 minutes
    public string CacheDirectory { get; set; } = ".cache";
}

public class Config
{
    public DownloaderConfig Downloader { get; set; } = new();
    public TmxConfig Tmx { get; set; } = new();
    public FixerConfig Fixer { get; set; } = new();
    public AppConfig App { get; set; } = new();
    public DesktopConfig Desktop { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();

    public static string DefaultMapsFolder => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Trackmania2020", "Maps", "Toolbox");

    public static Config Default
    {
        get
        {
            var config = new Config();
            var defaultMapsFolder = DefaultMapsFolder;
            config.Fixer.FolderPath = defaultMapsFolder;
            config.Desktop.BrowserFolder = defaultMapsFolder;
            return config;
        }
    }
}

[TomlSerializable(typeof(Config))]
internal partial class ToolboxConfigContext : TomlSerializerContext { }
