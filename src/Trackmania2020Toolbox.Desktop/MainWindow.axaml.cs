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


public partial class MainWindow : Window
{
    private readonly ToolboxApp _app;
    private readonly IBrowserService _browserService;
    private readonly IConfigService _configService;
    private readonly IFileSystem _fs;
    private Config _config;
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
    private readonly Grid _selectionOverlay;
    private readonly TextBlock _selectionTitle;
    private readonly ListBox _selectionList;
    private TaskCompletionSource<int>? _selectionTcs;
    private readonly TextBox _fixerFolderInput;
    private readonly TextBox _playerIdInput;
    private readonly TextBox _medalsCampaignInput;
    private readonly TextBox _gamePathInput;
    private readonly TextBox _browserFolderInput;
    private readonly NumericUpDown _downloadDelayMsInput;
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
        _config = Config.Default;
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
        _selectionOverlay = this.FindControl<Grid>("SelectionOverlay")!;
        _selectionTitle = this.FindControl<TextBlock>("SelectionTitle")!;
        _selectionList = this.FindControl<ListBox>("SelectionList")!;
        _fixerFolderInput = this.FindControl<TextBox>("FixerFolderInput")!;
        _playerIdInput = this.FindControl<TextBox>("PlayerIdInput")!;
        _medalsCampaignInput = this.FindControl<TextBox>("MedalsCampaignInput")!;
        _gamePathInput = this.FindControl<TextBox>("GamePathInput")!;
        _browserFolderInput = this.FindControl<TextBox>("BrowserFolderInput")!;
        _downloadDelayMsInput = this.FindControl<NumericUpDown>("DownloadDelayMsInput")!;
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

        var console = new LogConsole(AppendLog, selectionFunc: SelectItemAsync);
        var api = new TrackmaniaApiWrapper(TrackmaniaCLI.HttpClient, TrackmaniaCLI.UserAgent);
        _fs = new RealFileSystem();
        var net = new RealNetworkService(TrackmaniaCLI.HttpClient);
        var fixer = new RealMapFixer();
        var dateTime = new RealDateTime();
        _browserService = new RealBrowserService(_fs);
        _configService = new RealConfigService(_fs);

        Gbx.LZO = new Lzo();
        if (!TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Add("User-Agent", TrackmaniaCLI.UserAgent);

        _app = new ToolboxApp(api, _fs, net, fixer, console, dateTime, TrackmaniaCLI.GetScriptDirectory(), _configService);

        // Load initial settings
        _ = LoadConfigAsync();
        _fixerFolderInput.Text = _config.Fixer.FolderPath;
        _browserFolderInput.Text = _config.Desktop.BrowserFolder;
        _currentBrowserDirectory = _config.Desktop.BrowserFolder;
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
        this.FindControl<Button>("TmxSearchBtn")!.Click += async (_, _) =>
        {
            string sort = (_tmxSortCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "name";
            bool desc = (_tmxOrderCombo.SelectedIndex == 1);
            await RunTask(() => _app.HandleTmxSearch(_tmxSearchNameInput.Text, _tmxSearchAuthorInput.Text, sort, desc, GetConfig()));
        };

        this.FindControl<Button>("RunFixerBtn")!.Click += async (_, _) =>
        {
            AppendLog("Running batch fixer..." + Environment.NewLine);
            await Task.Run(() => _app.RunBatchFixer(GetConfig()));
        };
        this.FindControl<Button>("ExportMedalsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleExportCampaignMedals(_playerIdInput.Text ?? "", _medalsCampaignInput.Text, GetConfig()));
        this.FindControl<Button>("SaveSettingsBtn")!.Click += (_, _) => SaveConfig();
        this.FindControl<Button>("BrowseFixerBtn")!.Click += async (_, _) =>
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Fixer Folder",
                AllowMultiple = false
            });
            if (result.Count > 0) _fixerFolderInput.Text = result[0].Path.LocalPath;
        };
        this.FindControl<Button>("BrowseGameBtn")!.Click += async (_, _) =>
        {
            var result = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Trackmania.exe",
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Executables") { Patterns = new[] { "*.exe" } } },
                AllowMultiple = false
            });
            if (result.Count > 0) _gamePathInput.Text = result[0].Path.LocalPath;
        };
        this.FindControl<Button>("BrowseBrowserFolderBtn")!.Click += async (_, _) =>
        {
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
        _browserList.DoubleTapped += (_, _) =>
        {
            if (_doubleClickToPlayCheck.IsChecked ?? true) HandleBrowserAction();
        };
        _browserList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (_enterToPlayCheck.IsChecked ?? true) HandleBrowserAction();
                e.Handled = true;
            }
        };

        this.FindControl<TabControl>("MainTabControl")!.SelectionChanged += (s, e) =>
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem ti && ti.Header?.ToString() == "Browser")
            {
                RefreshBrowser();
            }
        };

        this.FindControl<Button>("SelectionConfirmBtn")!.Click += (_, _) =>
        {
            if (_selectionList.SelectedIndex >= 0)
            {
                _selectionTcs?.SetResult(_selectionList.SelectedIndex + 1);
                _selectionOverlay.IsVisible = false;
            }
        };

        this.FindControl<Button>("SelectionCancelBtn")!.Click += (_, _) =>
        {
            _selectionTcs?.SetResult(0);
            _selectionOverlay.IsVisible = false;
        };
    }

    private async Task<int> SelectItemAsync(string title, IEnumerable<string> items)
    {
        _selectionTcs = new TaskCompletionSource<int>();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _selectionTitle.Text = title;
            _selectionList.ItemsSource = items.ToList();
            _selectionList.SelectedIndex = -1;
            _selectionOverlay.IsVisible = true;
        });

        return await _selectionTcs.Task;
    }

    private void AppendLog(string text)
    {
        _logOutput.Text += text;
        _logOutput.CaretIndex = _logOutput.Text?.Length ?? 0;
    }

    private Config GetConfig()
    {
        var config = _configService.LoadConfig(_app._scriptDirectory);

        config.Fixer.FolderPath = _fixerFolderInput.Text ?? _app._defaultMapsFolder;
        config.Fixer.ExplicitFolder = true;
        config.Fixer.UpdateTitle = _updateTitleCheck.IsChecked ?? true;
        config.Fixer.ConvertPlatformMapType = _convertMapTypeCheck.IsChecked ?? true;
        config.Fixer.DryRun = _dryRunCheck.IsChecked ?? false;

        config.App.ForceOverwrite = _forceOverwriteCheck.IsChecked ?? false;
        config.App.Interactive = true;
        config.App.Play = _playAfterDownloadCheck.IsChecked ?? false;

        config.Desktop.BrowserFolder = _browserFolderInput.Text ?? _app._defaultMapsFolder;
        config.Desktop.DoubleClickToPlay = _doubleClickToPlayCheck.IsChecked ?? true;
        config.Desktop.EnterToPlay = _enterToPlayCheck.IsChecked ?? true;
        config.Desktop.PlayAfterDownload = _playAfterDownloadCheck.IsChecked ?? false;

        config.Downloader.DownloadDelayMs = (int)(_downloadDelayMsInput.Value ?? 1000);

        return config;
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
        if (string.IsNullOrEmpty(gamePath) || !_fs.FileExists(gamePath))
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

    private async Task LoadConfigAsync()
    {
        try
        {
            _config = await _configService.LoadConfigAsync(_app._scriptDirectory);
        }
        catch (Exception ex)
        {
            _config = Config.Default;
            AppendLog($"Error loading config: {ex.Message}. Using defaults.{Environment.NewLine}");
        }
        _gamePathInput.Text = _config.App.SetGamePath;
        _browserFolderInput.Text = _config.Desktop.BrowserFolder;
        _downloadDelayMsInput.Value = _config.Downloader.DownloadDelayMs;
        _doubleClickToPlayCheck.IsChecked = _config.Desktop.DoubleClickToPlay;
        _enterToPlayCheck.IsChecked = _config.Desktop.EnterToPlay;
        _playAfterDownloadCheck.IsChecked = _config.Desktop.PlayAfterDownload;
        _fixerFolderInput.Text = _config.Fixer.FolderPath;
        _currentBrowserDirectory = _config.Desktop.BrowserFolder;
        RefreshBrowser();
    }

    private async void SaveConfig()
    {
        try
        {
            _config.App.SetGamePath = _gamePathInput.Text;
            _config.Desktop.BrowserFolder = _browserFolderInput.Text ?? string.Empty;
            _config.Downloader.DownloadDelayMs = (int)(_downloadDelayMsInput.Value ?? 1000);
            _config.Desktop.DoubleClickToPlay = _doubleClickToPlayCheck.IsChecked ?? true;
            _config.Desktop.EnterToPlay = _enterToPlayCheck.IsChecked ?? true;
            _config.Desktop.PlayAfterDownload = _playAfterDownloadCheck.IsChecked ?? false;

            await _configService.SaveConfigAsync(_app._scriptDirectory, _config);

            AppendLog($"Settings saved to: {Path.Combine(_app._scriptDirectory, "config.toml")}{Environment.NewLine}");
            RefreshBrowser();
        }
        catch (Exception ex)
        {
            AppendLog($"Error saving config: {ex.Message}{Environment.NewLine}");
        }
    }

    private void RefreshBrowser()
    {
        if (string.IsNullOrEmpty(_currentBrowserDirectory) || !_fs.DirectoryExists(_currentBrowserDirectory))
        {
            _currentBrowserDirectory = _browserFolderInput.Text ?? _app._defaultMapsFolder;
        }

        if (!_fs.DirectoryExists(_currentBrowserDirectory))
        {
            try { _fs.CreateDirectory(_currentBrowserDirectory); } catch { return; }
        }

        _browserPathDisplay.Text = _currentBrowserDirectory;
        _browserItems.Clear();

        var filter = _browserSearchInput.Text ?? "";

        try
        {
            bool desc = _browserSortCombo.SelectedIndex == 1;
            var items = _browserService.GetBrowserItems(_currentBrowserDirectory, filter, desc);
            foreach (var item in items)
            {
                _browserItems.Add(item);
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
        if (!_fs.DirectoryExists(_currentBrowserDirectory)) return;

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
        var parent = Path.GetDirectoryName(_currentBrowserDirectory);
        if (!string.IsNullOrEmpty(parent))
        {
            _currentBrowserDirectory = parent;
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
