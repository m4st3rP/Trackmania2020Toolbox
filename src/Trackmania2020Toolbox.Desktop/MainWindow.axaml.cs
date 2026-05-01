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
    private Config _config = Config.Default;
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
    private readonly CheckBox _saveLastFolderCheck;
    private readonly CheckBox _saveLastSortCheck;
    private readonly CheckBox _cacheEnabledCheck;
    private readonly NumericUpDown _staticCacheExpiryInput;
    private readonly NumericUpDown _dynamicCacheExpiryInput;
    private readonly NumericUpDown _highlyDynamicCacheExpiryInput;
    private readonly ProgressBar _busyIndicator;
    private bool _isBusy;

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
        _saveLastFolderCheck = this.FindControl<CheckBox>("SaveLastFolderCheck")!;
        _saveLastSortCheck = this.FindControl<CheckBox>("SaveLastSortCheck")!;
        _cacheEnabledCheck = this.FindControl<CheckBox>("CacheEnabledCheck")!;
        _staticCacheExpiryInput = this.FindControl<NumericUpDown>("StaticCacheExpiryInput")!;
        _dynamicCacheExpiryInput = this.FindControl<NumericUpDown>("DynamicCacheExpiryInput")!;
        _highlyDynamicCacheExpiryInput = this.FindControl<NumericUpDown>("HighlyDynamicCacheExpiryInput")!;
        _busyIndicator = this.FindControl<ProgressBar>("BusyIndicator")!;

        _browserList = this.FindControl<ListBox>("BrowserList")!;
        _browserPathDisplay = this.FindControl<TextBox>("BrowserPathDisplay")!;
        _browserSearchInput = this.FindControl<TextBox>("BrowserSearchInput")!;
        _browserList.ItemsSource = _browserItems;

        var console = new LogConsole(AppendLog, selectionFunc: SelectItemAsync);
        _fs = new RealFileSystem();
        var scriptDir = TrackmaniaCLI.GetScriptDirectory();
        _configService = new RealConfigService(_fs, console);
        _config = _configService.LoadConfig(scriptDir);

        var rawApi = new TrackmaniaApiWrapper(TrackmaniaCLI.HttpClient, TrackmaniaCLI.UserAgent);
        var api = new CachedTrackmaniaApi(rawApi, _fs, scriptDir, _config.Cache);
        var net = new RealNetworkService(TrackmaniaCLI.HttpClient);
        var fixer = new RealMapFixer();
        var dateTime = new RealDateTime();
        _browserService = new RealBrowserService(_fs);

        Gbx.LZO = new Lzo();
        if (!TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Add("User-Agent", TrackmaniaCLI.UserAgent);

        _app = new ToolboxApp(api, _fs, net, fixer, console, dateTime, scriptDir, _configService);

        // Load initial settings
        UpdateUiFromConfig();

        // Wire up buttons
        this.FindControl<Button>("WeeklyShortsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleWeeklyShortsAsync(_weeklyShortsInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("WeeklyGrandsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleWeeklyGrandsAsync(_weeklyGrandsInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("SeasonalBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleSeasonalAsync(_seasonalInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("ToTDBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTrackOfTheDayAsync(_toTDInput.Text ?? "latest", GetConfig()));
        this.FindControl<Button>("ClubBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleClubCampaignAsync(_clubInput.Text ?? "", GetConfig()));
        this.FindControl<Button>("TmxDownloadBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTmxMapsAsync(_tmxInput.Text ?? "", GetConfig()));
        this.FindControl<Button>("TmxPackBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTmxPacksAsync(_tmxPackInput.Text ?? "", GetConfig()));
        this.FindControl<Button>("TmxRandomBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleTmxRandomAsync(GetConfig()));
        this.FindControl<Button>("TmxSearchBtn")!.Click += async (_, _) =>
        {
            string sort = (_tmxSortCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "name";
            bool desc = (_tmxOrderCombo.SelectedIndex == 1);
            await RunTask(() => _app.HandleTmxSearchAsync(_tmxSearchNameInput.Text, _tmxSearchAuthorInput.Text, sort, desc, GetConfig()));
        };

        this.FindControl<Button>("RunFixerBtn")!.Click += async (_, _) =>
        {
            await RunTask(async () =>
            {
                AppendLog("Running batch fixer..." + Environment.NewLine);
                await _app.RunBatchFixerAsync(GetConfig());
            });
        };
        this.FindControl<Button>("ExportMedalsBtn")!.Click += async (_, _) => await RunTask(() => _app.HandleExportCampaignMedalsAsync(_playerIdInput.Text ?? "", _medalsCampaignInput.Text, GetConfig()));
        this.FindControl<Button>("SaveSettingsBtn")!.Click += (_, _) => SaveConfig();
        this.FindControl<Button>("ResetSettingsBtn")!.Click += async (_, _) =>
        {
            var choice = await SelectItemAsync("Reset All Settings?", new[] { "Yes, reset everything", "No, keep my settings" });
            if (choice == 1) // "Yes, reset everything" is index 1 because SelectItemAsync returns 1-based index (0 is cancel)
            {
                await ResetConfigAsync();
            }
        };
        this.FindControl<Button>("ResetCacheBtn")!.Click += async (_, _) =>
        {
            var choice = await SelectItemAsync("Reset API Cache?", new[] { "Yes, clear all cached API data", "No, keep cache" });
            if (choice == 1) // "Yes, clear all cached API data" is index 1
            {
                if (_app.Api is CachedTrackmaniaApi cachedApi)
                {
                    cachedApi.ResetCache();
                    AppendLog($"API Cache cleared.{Environment.NewLine}");
                }
            }
        };
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

    /// <summary>
    /// Displays a selection overlay and returns the 1-based index of the selected item.
    /// Returns 0 if cancelled.
    /// </summary>
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
        Dispatcher.UIThread.Post(() =>
        {
            _logOutput.Text += text;
            _logOutput.CaretIndex = _logOutput.Text?.Length ?? 0;
        });
    }

    private Config GetConfig()
    {
        _config.Fixer.FolderPath = _fixerFolderInput.Text ?? _app.DefaultMapsFolder;
        _config.Fixer.ExplicitFolder = true;
        _config.Fixer.UpdateTitle = _updateTitleCheck.IsChecked ?? true;
        _config.Fixer.ConvertPlatformMapType = _convertMapTypeCheck.IsChecked ?? true;
        _config.Fixer.DryRun = _dryRunCheck.IsChecked ?? false;

        _config.App.ForceOverwrite = _forceOverwriteCheck.IsChecked ?? false;
        _config.App.Interactive = true;
        _config.App.Play = _playAfterDownloadCheck.IsChecked ?? false;
        _config.App.SetGamePath = _gamePathInput.Text;

        _config.Desktop.BrowserFolder = _browserFolderInput.Text ?? _app.DefaultMapsFolder;
        _config.Desktop.DoubleClickToPlay = _doubleClickToPlayCheck.IsChecked ?? true;
        _config.Desktop.EnterToPlay = _enterToPlayCheck.IsChecked ?? true;
        _config.Desktop.PlayAfterDownload = _playAfterDownloadCheck.IsChecked ?? false;
        _config.Desktop.SaveLastFolder = _saveLastFolderCheck.IsChecked ?? false;
        _config.Desktop.LastFolder = _currentBrowserDirectory;
        _config.Desktop.SaveLastSort = _saveLastSortCheck.IsChecked ?? false;
        _config.Desktop.LastSort = _browserSortCombo.SelectedIndex;

        _config.Downloader.DownloadDelayMs = (int)(_downloadDelayMsInput.Value ?? 1000);

        _config.Cache.Enabled = _cacheEnabledCheck.IsChecked ?? true;
        _config.Cache.StaticExpirationMinutes = (int)(_staticCacheExpiryInput.Value ?? 43200);
        _config.Cache.DynamicExpirationMinutes = (int)(_dynamicCacheExpiryInput.Value ?? 60);
        _config.Cache.HighlyDynamicExpirationMinutes = (int)(_highlyDynamicCacheExpiryInput.Value ?? 5);

        return _config;
    }

    private async Task RunTask(Func<Task> task)
    {
        if (_isBusy) return;
        SetBusy(true);
        try
        {
            await task();
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}{Environment.NewLine}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunTask(Func<Task<List<string>>> task)
    {
        if (_isBusy) return;
        SetBusy(true);
        try
        {
            var paths = await task();
            if (paths.Count > 0)
            {
                RefreshBrowser();
                if (_playAfterDownloadCheck.IsChecked ?? false)
                {
                    _app.LaunchGame(paths);
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}{Environment.NewLine}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busyIndicator.IsVisible = busy;
    }

    private async Task LoadConfigAsync()
    {
        _config = await _configService.LoadConfigAsync(_app.ScriptDirectory);
        UpdateUiFromConfig();
    }

    private void UpdateUiFromConfig()
    {
        _gamePathInput.Text = _config.App.SetGamePath;
        _browserFolderInput.Text = _config.Desktop.BrowserFolder;
        _downloadDelayMsInput.Value = _config.Downloader.DownloadDelayMs;
        _doubleClickToPlayCheck.IsChecked = _config.Desktop.DoubleClickToPlay;
        _enterToPlayCheck.IsChecked = _config.Desktop.EnterToPlay;
        _playAfterDownloadCheck.IsChecked = _config.Desktop.PlayAfterDownload;
        _saveLastFolderCheck.IsChecked = _config.Desktop.SaveLastFolder;
        _saveLastSortCheck.IsChecked = _config.Desktop.SaveLastSort;
        _fixerFolderInput.Text = _config.Fixer.FolderPath;

        _cacheEnabledCheck.IsChecked = _config.Cache.Enabled;
        _staticCacheExpiryInput.Value = _config.Cache.StaticExpirationMinutes;
        _dynamicCacheExpiryInput.Value = _config.Cache.DynamicExpirationMinutes;
        _highlyDynamicCacheExpiryInput.Value = _config.Cache.HighlyDynamicExpirationMinutes;

        if (_config.Desktop.SaveLastFolder && !string.IsNullOrEmpty(_config.Desktop.LastFolder) && _fs.DirectoryExists(_config.Desktop.LastFolder))
        {
            _currentBrowserDirectory = _config.Desktop.LastFolder;
        }
        else
        {
            _currentBrowserDirectory = _config.Desktop.BrowserFolder;
        }

        if (_config.Desktop.SaveLastSort)
        {
            _browserSortCombo.SelectedIndex = _config.Desktop.LastSort;
        }

        RefreshBrowser();
    }

    private async void SaveConfig()
    {
        try
        {
            _config = GetConfig();
            await _configService.SaveConfigAsync(_app.ScriptDirectory, _config);

            AppendLog($"Settings saved to: {Path.Combine(_app.ScriptDirectory, "config.toml")}{Environment.NewLine}");
            RefreshBrowser();
        }
        catch (Exception ex)
        {
            AppendLog($"Error saving config: {ex.Message}{Environment.NewLine}");
        }
    }

    private async Task ResetConfigAsync()
    {
        try
        {
            _config = Config.Default;
            await _configService.SaveConfigAsync(_app.ScriptDirectory, _config);
            await LoadConfigAsync();
            AppendLog($"Settings reset to defaults and saved.{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            AppendLog($"Error resetting config: {ex.Message}{Environment.NewLine}");
        }
    }

    private async Task RefreshBrowserAsync()
    {
        if (string.IsNullOrEmpty(_currentBrowserDirectory) || !_fs.DirectoryExists(_currentBrowserDirectory))
        {
            _currentBrowserDirectory = _browserFolderInput.Text ?? _app.DefaultMapsFolder;
        }

        if (!_fs.DirectoryExists(_currentBrowserDirectory))
        {
            try { _fs.CreateDirectory(_currentBrowserDirectory); } catch { return; }
        }

        if (_saveLastFolderCheck != null && _saveLastFolderCheck.IsChecked == true)
        {
            _config.Desktop.LastFolder = _currentBrowserDirectory;
            await _configService.SaveConfigAsync(_app.ScriptDirectory, _config);
        }

        _browserPathDisplay.Text = _currentBrowserDirectory;
        _browserItems.Clear();

        var filter = _browserSearchInput.Text ?? "";

        try
        {
            if (_saveLastSortCheck != null && _saveLastSortCheck.IsChecked == true)
            {
                _config.Desktop.LastSort = _browserSortCombo.SelectedIndex;
                await _configService.SaveConfigAsync(_app.ScriptDirectory, _config);
            }

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

    private void RefreshBrowser() => _ = RefreshBrowserAsync();

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
                _app.LaunchGame([item.FullPath]);
            }
        }
    }

    private void PlaySelectedMap()
    {
        if (_browserList.SelectedItem is BrowserItem item && !item.IsDirectory)
        {
            _app.LaunchGame([item.FullPath]);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
