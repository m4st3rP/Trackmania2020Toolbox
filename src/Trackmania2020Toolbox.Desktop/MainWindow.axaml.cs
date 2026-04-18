using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Trackmania2020Toolbox;
using GBX.NET;
using GBX.NET.LZO;

namespace Trackmania2020Toolbox.Desktop;

public partial class MainWindow : Window
{
    private readonly ToolboxApp _app;
    private readonly TextBox _logOutput;
    private readonly TextBox _weeklyShortsInput;
    private readonly TextBox _weeklyGrandsInput;
    private readonly TextBox _seasonalInput;
    private readonly TextBox _toTDInput;
    private readonly TextBox _clubInput;
    private readonly TextBox _fixerFolderInput;
    private readonly TextBox _playerIdInput;
    private readonly TextBox _medalsCampaignInput;
    private readonly TextBox _gamePathInput;
    private readonly CheckBox _updateTitleCheck;
    private readonly CheckBox _convertMapTypeCheck;
    private readonly CheckBox _dryRunCheck;
    private readonly CheckBox _forceOverwriteCheck;
    private readonly CheckBox _playAfterDownloadCheck;

    public MainWindow()
    {
        InitializeComponent();

        _logOutput = this.FindControl<TextBox>("LogOutput")!;
        _weeklyShortsInput = this.FindControl<TextBox>("WeeklyShortsInput")!;
        _weeklyGrandsInput = this.FindControl<TextBox>("WeeklyGrandsInput")!;
        _seasonalInput = this.FindControl<TextBox>("SeasonalInput")!;
        _toTDInput = this.FindControl<TextBox>("ToTDInput")!;
        _clubInput = this.FindControl<TextBox>("ClubInput")!;
        _fixerFolderInput = this.FindControl<TextBox>("FixerFolderInput")!;
        _playerIdInput = this.FindControl<TextBox>("PlayerIdInput")!;
        _medalsCampaignInput = this.FindControl<TextBox>("MedalsCampaignInput")!;
        _gamePathInput = this.FindControl<TextBox>("GamePathInput")!;
        _updateTitleCheck = this.FindControl<CheckBox>("UpdateTitleCheck")!;
        _convertMapTypeCheck = this.FindControl<CheckBox>("ConvertMapTypeCheck")!;
        _dryRunCheck = this.FindControl<CheckBox>("DryRunCheck")!;
        _forceOverwriteCheck = this.FindControl<CheckBox>("ForceOverwriteCheck")!;
        _playAfterDownloadCheck = this.FindControl<CheckBox>("PlayAfterDownloadCheck")!;

        var console = new LogConsole(AppendLog);
        var api = new TrackmaniaApiWrapper(TrackmaniaCLI.UserAgent);
        var fs = new RealFileSystem();
        var net = new RealNetworkService(TrackmaniaCLI.HttpClient);
        var fixer = new RealMapFixer();
        var dateTime = new RealDateTime();

        Gbx.LZO = new Lzo();
        if (!TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Add("User-Agent", TrackmaniaCLI.UserAgent);

        _app = new ToolboxApp(api, fs, net, fixer, console, dateTime, TrackmaniaCLI.GetScriptDirectory());

        // Load initial settings
        _fixerFolderInput.Text = _app._defaultMapsFolder;
        LoadGamePath();

        // Wire up buttons
        this.FindControl<Button>("WeeklyShortsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleWeeklyShorts(_weeklyShortsInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("WeeklyGrandsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleWeeklyGrands(_weeklyGrandsInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("SeasonalBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleSeasonal(_seasonalInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("ToTDBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTrackOfTheDay(_toTDInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("ClubBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleClubCampaign(_clubInput.Text ?? "", GetConfig()));
        this.FindControl<Button>("RunFixerBtn")!.Click += async (_, _) => {
             AppendLog("Running batch fixer..." + Environment.NewLine);
             await Task.Run(() => _app.RunBatchFixer(GetConfig()));
        };
        this.FindControl<Button>("ExportMedalsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleExportCampaignMedals(_playerIdInput.Text ?? "", _medalsCampaignInput.Text));
        this.FindControl<Button>("SaveSettingsBtn")!.Click += (_, _) => SaveGamePath(_gamePathInput.Text ?? "");
        this.FindControl<Button>("BrowseFixerBtn")!.Click += async (_, _) => {
            var result = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Fixer Folder",
                AllowMultiple = false
            });
            if (result.Count > 0) _fixerFolderInput.Text = result[0].Path.LocalPath;
        };
        this.FindControl<Button>("BrowseGameBtn")!.Click += async (_, _) => {
            var result = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Trackmania.exe",
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Executables") { Patterns = new[] { "*.exe" } } },
                AllowMultiple = false
            });
            if (result.Count > 0) _gamePathInput.Text = result[0].Path.LocalPath;
        };
    }

    private void AppendLog(string text)
    {
        _logOutput.Text += text;
        _logOutput.CaretIndex = _logOutput.Text?.Length ?? 0;
    }

    private Config GetConfig()
    {
        return new Config(
            null, null, null, null, null, null, null,
            _fixerFolderInput.Text ?? _app._defaultMapsFolder,
            true, // Use explicit folder
            _updateTitleCheck.IsChecked ?? true,
            _convertMapTypeCheck.IsChecked ?? true,
            _dryRunCheck.IsChecked ?? false,
            _forceOverwriteCheck.IsChecked ?? false,
            false, // non-interactive
            _playAfterDownloadCheck.IsChecked ?? false,
            null,
            new List<string>()
        );
    }

    private async Task RunTask(Func<Task> task)
    {
        try
        {
            await task();
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}{Environment.NewLine}");
        }
    }

    private async Task RunTask(Func<Task<List<string>>> task)
    {
        try
        {
            var paths = await task();
            if ((_playAfterDownloadCheck.IsChecked ?? false) && paths.Count > 0)
            {
                // We need to call LaunchGame but it's private in ToolboxApp.
                // However, RunAsync handles it if config.Play is true.
                // But we already ran the specific handler.
                // Let's use reflection to call LaunchGame or just re-implement it briefly.
                LaunchGame(paths);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}{Environment.NewLine}");
        }
    }

    private void LaunchGame(List<string> paths)
    {
        // Re-implementing LaunchGame since it's private in ToolboxApp
        var gamePath = _gamePathInput.Text;
        if (string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath))
        {
            AppendLog("Error: Trackmania.exe path not set or invalid." + Environment.NewLine);
            return;
        }

        var sortedMaps = paths.Distinct().OrderBy(p => p).ToList();
        if (sortedMaps.Count == 0) return;

        var firstMap = sortedMaps.First();
        var arguments = $"\"{Path.GetFullPath(firstMap)}\"";
        try
        {
            AppendLog($"Launching Trackmania with {Path.GetFileName(firstMap)}..." + Environment.NewLine);
            System.Diagnostics.Process.Start(gamePath, arguments);
        }
        catch (Exception ex)
        {
            AppendLog($"Error launching game: {ex.Message}" + Environment.NewLine);
        }
    }

    private void LoadGamePath()
    {
        var configPath = Path.Combine(_app._scriptDirectory, "config.toml");
        if (!File.Exists(configPath)) return;
        var lines = File.ReadAllLines(configPath);
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("game_path", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2) _gamePathInput.Text = parts[1].Trim().Trim('"');
            }
        }
    }

    private void SaveGamePath(string path)
    {
        var configPath = Path.Combine(_app._scriptDirectory, "config.toml");
        try
        {
            File.WriteAllText(configPath, $"game_path = \"{path}\"\n");
            AppendLog($"Game path saved to: {configPath}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            AppendLog($"Error saving config: {ex.Message}{Environment.NewLine}");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
