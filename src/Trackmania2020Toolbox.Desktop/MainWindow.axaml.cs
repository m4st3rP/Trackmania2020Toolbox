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
    private readonly IConfigService _configService;
    private readonly IBrowserService _browserService;
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
    private Config _config = Config.Default;

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
        var fs = new RealFileSystem();
        var net = new RealNetworkService(TrackmaniaCLI.HttpClient);
        var fixer = new RealMapFixer();
        var dateTime = new RealDateTime();
        _configService = new RealConfigService(TrackmaniaCLI.GetScriptDirectory(), fs);
        _browserService = new RealBrowserService(fs);

        Gbx.LZO = new Lzo();
        if (!TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            TrackmaniaCLI.HttpClient.DefaultRequestHeaders.Add("User-Agent", TrackmaniaCLI.UserAgent);

        _app = new ToolboxApp(api, fs, net, fixer, console, dateTime, TrackmaniaCLI.GetScriptDirectory());

        // Load initial settings
        _ = InitializeAsync();

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
        return _config with
        {
            Fixer = _config.Fixer with
            {
                FolderPath = _fixerFolderInput.Text ?? _app._defaultMapsFolder,
                ExplicitFolder = true,
                UpdateTitle = _updateTitleCheck.IsChecked ?? true,
                ConvertPlatformMapType = _convertMapTypeCheck.IsChecked ?? true,
                DryRun = _dryRunCheck.IsChecked ?? false
            },
            App = _config.App with
            {
                ForceOverwrite = _forceOverwriteCheck.IsChecked ?? false,
                Interactive = true,
                Play = _playAfterDownloadCheck.IsChecked ?? false
            }
        };
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

    private async Task InitializeAsync()
    {
        _config = await _configService.LoadConfigAsync();
        _fixerFolderInput.Text = _config.Fixer.FolderPath;
        _browserFolderInput.Text = _config.Desktop.BrowserFolder;
        _gamePathInput.Text = _config.Desktop.GamePath;
        _doubleClickToPlayCheck.IsChecked = _config.Desktop.DoubleClickToPlay;
        _enterToPlayCheck.IsChecked = _config.Desktop.EnterToPlay;
        _currentBrowserDirectory = _config.Desktop.BrowserFolder;
        RefreshBrowser();
    }

    private async void SaveConfig()
    {
        _config = _config with
        {
            Fixer = _config.Fixer with { FolderPath = _fixerFolderInput.Text ?? "" },
            Desktop = _config.Desktop with
            {
                GamePath = _gamePathInput.Text ?? "",
                BrowserFolder = _browserFolderInput.Text ?? "",
                DoubleClickToPlay = _doubleClickToPlayCheck.IsChecked ?? true,
                EnterToPlay = _enterToPlayCheck.IsChecked ?? true
            }
        };

        await _configService.SaveConfigAsync(_config);
        AppendLog("Settings saved." + Environment.NewLine);
        RefreshBrowser();
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
        bool descending = _browserSortCombo.SelectedIndex == 1;

        var items = _browserService.GetItems(_currentBrowserDirectory, filter, descending);
        foreach (var item in items)
        {
            _browserItems.Add(item);
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
