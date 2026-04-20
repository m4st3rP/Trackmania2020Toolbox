using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Trackmania2020Toolbox;
using GBX.NET;
using GBX.NET.LZO;
using TmEssentials;

namespace Trackmania2020Toolbox.Desktop;

public class BrowserItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string Icon => IsDirectory ? "📁" : "📄";
}

public partial class MainWindow : Window
{
    private readonly ToolboxApp _app;
    private readonly TextBox _logOutput;
    private readonly TextBox _weeklyShortsInput;
    private readonly TextBox _weeklyGrandsInput;
    private readonly TextBox _seasonalInput;
    private readonly TextBox _toTDInput;
    private readonly TextBox _clubInput;
    private readonly TextBox _tmxInput;
    private readonly TextBox _tmxPackInput;
    private readonly TextBox _tmxSearchNameInput;
    private readonly TextBox _tmxSearchAuthorInput;
    private readonly ComboBox _tmxSortCombo;
    private readonly ComboBox _tmxOrderCombo;
    private readonly ComboBox _browserSortCombo;
    private readonly TextBox _fixerFolderInput;
    private readonly TextBox _playerIdInput;
    private readonly TextBox _medalsCampaignInput;
    private readonly TextBox _gamePathInput;
    private readonly TextBox _browserFolderInput;
    private readonly CheckBox _updateTitleCheck;
    private readonly CheckBox _convertMapTypeCheck;
    private readonly CheckBox _dryRunCheck;
    private readonly CheckBox _forceOverwriteCheck;
    private readonly CheckBox _playAfterDownloadCheck;
    private readonly CheckBox _doubleClickToPlayCheck;
    private readonly CheckBox _enterToPlayCheck;

    private readonly ListBox _browserList;
    private readonly TextBox _browserPathDisplay;
    private readonly TextBox _browserSearchInput;
    private readonly ObservableCollection<BrowserItem> _browserItems = new();
    private string _currentBrowserDirectory = string.Empty;
    private FileSystemWatcher? _watcher;

    public MainWindow()
    {
        InitializeComponent();

        _logOutput = this.FindControl<TextBox>("LogOutput")!;
        _weeklyShortsInput = this.FindControl<TextBox>("WeeklyShortsInput")!;
        _weeklyGrandsInput = this.FindControl<TextBox>("WeeklyGrandsInput")!;
        _seasonalInput = this.FindControl<TextBox>("SeasonalInput")!;
        _toTDInput = this.FindControl<TextBox>("ToTDInput")!;
        _clubInput = this.FindControl<TextBox>("ClubInput")!;
        _tmxInput = this.FindControl<TextBox>("TmxInput")!;
        _tmxPackInput = this.FindControl<TextBox>("TmxPackInput")!;
        _tmxSearchNameInput = this.FindControl<TextBox>("TmxSearchNameInput")!;
        _tmxSearchAuthorInput = this.FindControl<TextBox>("TmxSearchAuthorInput")!;
        _tmxSortCombo = this.FindControl<ComboBox>("TmxSortCombo")!;
        _tmxOrderCombo = this.FindControl<ComboBox>("TmxOrderCombo")!;
        _browserSortCombo = this.FindControl<ComboBox>("BrowserSortCombo")!;
        _fixerFolderInput = this.FindControl<TextBox>("FixerFolderInput")!;
        _playerIdInput = this.FindControl<TextBox>("PlayerIdInput")!;
        _medalsCampaignInput = this.FindControl<TextBox>("MedalsCampaignInput")!;
        _gamePathInput = this.FindControl<TextBox>("GamePathInput")!;
        _browserFolderInput = this.FindControl<TextBox>("BrowserFolderInput")!;
        _updateTitleCheck = this.FindControl<CheckBox>("UpdateTitleCheck")!;
        _convertMapTypeCheck = this.FindControl<CheckBox>("ConvertMapTypeCheck")!;
        _dryRunCheck = this.FindControl<CheckBox>("DryRunCheck")!;
        _forceOverwriteCheck = this.FindControl<CheckBox>("ForceOverwriteCheck")!;
        _playAfterDownloadCheck = this.FindControl<CheckBox>("PlayAfterDownloadCheck")!;
        _doubleClickToPlayCheck = this.FindControl<CheckBox>("DoubleClickToPlayCheck")!;
        _enterToPlayCheck = this.FindControl<CheckBox>("EnterToPlayCheck")!;

        _browserList = this.FindControl<ListBox>("BrowserList")!;
        _browserPathDisplay = this.FindControl<TextBox>("BrowserPathDisplay")!;
        _browserSearchInput = this.FindControl<TextBox>("BrowserSearchInput")!;
        _browserList.ItemsSource = _browserItems;

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
        _browserFolderInput.Text = _app._defaultMapsFolder;
        LoadConfig();
        _currentBrowserDirectory = _browserFolderInput.Text;
        RefreshBrowser();

        // Wire up buttons
        this.FindControl<Button>("WeeklyShortsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleWeeklyShorts(_weeklyShortsInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("WeeklyGrandsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleWeeklyGrands(_weeklyGrandsInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("SeasonalBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleSeasonal(_seasonalInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("ToTDBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTrackOfTheDay(_toTDInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("ClubBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleClubCampaign(_clubInput.Text ?? "", GetConfig()));
        this.FindControl<Button>("TmxDownloadBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTmxMaps(_tmxInput.Text ?? "", GetConfig()));
        this.FindControl<Button>("TmxPackBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTmxPacks(_tmxPackInput.Text ?? "", GetConfig()));
        this.FindControl<Button>("TmxRandomBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTmxRandom(GetConfig()));
        this.FindControl<Button>("TmxSearchBtn")!.Click += async (_, _) => {
            string sort = (_tmxSortCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "name";
            bool desc = (_tmxOrderCombo.SelectedIndex == 1);
            await RunTask(() => _app.HandleTmxSearch(_tmxSearchNameInput.Text, _tmxSearchAuthorInput.Text, sort, desc, GetConfig()));
        };

        this.FindControl<Button>("RunFixerBtn")!.Click += async (_, _) => {
             AppendLog("Running batch fixer..." + Environment.NewLine);
             await Task.Run(() => _app.RunBatchFixer(GetConfig()));
        };
        this.FindControl<Button>("ExportMedalsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleExportCampaignMedals(_playerIdInput.Text ?? "", _medalsCampaignInput.Text));
        this.FindControl<Button>("SaveSettingsBtn")!.Click += (_, _) => SaveConfig();
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
        this.FindControl<Button>("BrowseBrowserFolderBtn")!.Click += async (_, _) => {
            var result = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Browser Folder",
                AllowMultiple = false
            });
            if (result.Count > 0) _browserFolderInput.Text = result[0].Path.LocalPath;
        };

        this.FindControl<Button>("BrowserUpBtn")!.Click += (_, _) => NavigateUp();
        this.FindControl<Button>("BrowserRefreshBtn")!.Click += (_, _) => RefreshBrowser();
        _browserSearchInput.TextChanged += (_, _) => RefreshBrowser();
        _browserSortCombo.SelectionChanged += (_, _) => RefreshBrowser();
        this.FindControl<Button>("BrowserPlayBtn")!.Click += (_, _) => PlaySelectedMap();
        _browserList.DoubleTapped += (_, _) => {
            if (_doubleClickToPlayCheck.IsChecked ?? true) HandleBrowserAction();
        };
        _browserList.KeyDown += (_, e) => {
            if (e.Key == Key.Enter) {
                if (_enterToPlayCheck.IsChecked ?? true) HandleBrowserAction();
                e.Handled = true;
            }
        };

        this.FindControl<TabControl>("MainTabControl")!.SelectionChanged += (s, e) => {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem ti && ti.Header?.ToString() == "Browser")
            {
                RefreshBrowser();
            }
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
            null, null, null, null, null,
            null, null, null, null, "name", false, false,
            null, null,
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
            if (paths.Count > 0)
            {
                RefreshBrowser();
                if (_playAfterDownloadCheck.IsChecked ?? false)
                {
                    LaunchGame(paths);
                }
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

    private void LoadConfig()
    {
        var configPath = Path.Combine(_app._scriptDirectory, "config.toml");
        if (!File.Exists(configPath)) return;
        var lines = File.ReadAllLines(configPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || !trimmed.Contains('=')) continue;

            var parts = trimmed.Split('=', 2);
            var key = parts[0].Trim().ToLowerInvariant();
            var value = parts[1].Trim().Trim('"');

            switch (key)
            {
                case "game_path": _gamePathInput.Text = value; break;
                case "browser_folder": _browserFolderInput.Text = value; break;
                case "double_click_to_play": _doubleClickToPlayCheck.IsChecked = bool.Parse(value); break;
                case "enter_to_play": _enterToPlayCheck.IsChecked = bool.Parse(value); break;
            }
        }
    }

    private void SaveConfig()
    {
        var configPath = Path.Combine(_app._scriptDirectory, "config.toml");
        try
        {
            var lines = new List<string>
            {
                $"game_path = \"{_gamePathInput.Text}\"",
                $"browser_folder = \"{_browserFolderInput.Text}\"",
                $"double_click_to_play = {(_doubleClickToPlayCheck.IsChecked ?? false).ToString().ToLower()}",
                $"enter_to_play = {(_enterToPlayCheck.IsChecked ?? false).ToString().ToLower()}"
            };
            File.WriteAllLines(configPath, lines);
            AppendLog($"Settings saved to: {configPath}{Environment.NewLine}");

            // If browser folder changed and we are in the root of old browser folder, update it
            // For simplicity, let's just refresh if we are currently at or under the browser folder
            RefreshBrowser();
        }
        catch (Exception ex)
        {
            AppendLog($"Error saving config: {ex.Message}{Environment.NewLine}");
        }
    }

    private void RefreshBrowser()
    {
        if (string.IsNullOrEmpty(_currentBrowserDirectory) || !Directory.Exists(_currentBrowserDirectory))
        {
            _currentBrowserDirectory = _browserFolderInput.Text ?? _app._defaultMapsFolder;
        }

        if (!Directory.Exists(_currentBrowserDirectory))
        {
            try { Directory.CreateDirectory(_currentBrowserDirectory); } catch { return; }
        }

        _browserPathDisplay.Text = _currentBrowserDirectory;
        _browserItems.Clear();

        var filter = _browserSearchInput.Text ?? "";

        try
        {
            bool desc = _browserSortCombo.SelectedIndex == 1;

            var dirs = Directory.GetDirectories(_currentBrowserDirectory)
                .Select(d => new { Path = d, Name = Path.GetFileName(d) });

            var sortedDirs = desc ? dirs.OrderByDescending(d => d.Name) : dirs.OrderBy(d => d.Name);

            foreach (var dir in sortedDirs)
            {
                if (!string.IsNullOrEmpty(filter) && !dir.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                _browserItems.Add(new BrowserItem {
                    DisplayName = dir.Name,
                    FullPath = dir.Path,
                    IsDirectory = true
                });
            }

            var files = Directory.GetFiles(_currentBrowserDirectory, "*.Map.Gbx")
                .Select(f => {
                    var fn = Path.GetFileName(f);
                    return new { Path = f, FileName = fn, DisplayName = TextFormatter.Deformat(fn) };
                });

            var filteredFiles = files.Where(f =>
                string.IsNullOrEmpty(filter) ||
                f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));

            var sortedFiles = desc ? filteredFiles.OrderByDescending(f => f.DisplayName) : filteredFiles.OrderBy(f => f.DisplayName);

            foreach (var file in sortedFiles)
            {
                _browserItems.Add(new BrowserItem {
                    DisplayName = file.DisplayName,
                    FullPath = file.Path,
                    IsDirectory = false
                });
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error refreshing browser: {ex.Message}{Environment.NewLine}");
        }

        SetupWatcher();
    }

    private void SetupWatcher()
    {
        _watcher?.Dispose();
        if (!Directory.Exists(_currentBrowserDirectory)) return;

        _watcher = new FileSystemWatcher(_currentBrowserDirectory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.*"
        };

        _watcher.Created += (s, e) => Dispatcher.UIThread.Post(RefreshBrowser);
        _watcher.Deleted += (s, e) => Dispatcher.UIThread.Post(RefreshBrowser);
        _watcher.Renamed += (s, e) => Dispatcher.UIThread.Post(RefreshBrowser);
        _watcher.EnableRaisingEvents = true;
    }

    private void NavigateUp()
    {
        var parent = Directory.GetParent(_currentBrowserDirectory);
        if (parent != null)
        {
            _currentBrowserDirectory = parent.FullName;
            RefreshBrowser();
        }
    }

    private void HandleBrowserAction()
    {
        if (_browserList.SelectedItem is BrowserItem item)
        {
            if (item.IsDirectory)
            {
                _currentBrowserDirectory = item.FullPath;
                RefreshBrowser();
            }
            else
            {
                LaunchGame(new List<string> { item.FullPath });
            }
        }
    }

    private void PlaySelectedMap()
    {
        if (_browserList.SelectedItem is BrowserItem item && !item.IsDirectory)
        {
            LaunchGame(new List<string> { item.FullPath });
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
