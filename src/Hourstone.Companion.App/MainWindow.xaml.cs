using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Data.Common;
using System.Security;
using System.Windows.Input;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Hourstone.Companion.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
namespace Hourstone.Companion.App;

public partial class MainWindow : Window
{
    readonly MainViewModel vm;
    readonly CompanionService? service;
    readonly bool demo, render;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly List<FileSystemWatcher> watchers = [];
    readonly Forms.NotifyIcon? tray;
    readonly EventWaitHandle? showSignal;
    readonly RegisteredWaitHandle? showWait;
    readonly CancellationTokenSource stopping = new();
    readonly UpdateCoordinator? updater;
    readonly SyncNoticeTracker syncNotifications = new();
    SyncResult? displayedSyncResult;
    IReadOnlyList<SourceConfiguration>? sourcePreview;
    IReadOnlyList<LocalSourceStatus>? sourceStatusPreview;
    string? syncPreviewState;
    WindowWorkArea? windowWorkArea;
    UserSettings preferences;
    SettingsDraft? settingsDraft;
    bool savingSettings;
    DateTimeOffset lastScan = DateTimeOffset.MinValue, changed = DateTimeOffset.MaxValue;
    bool quit, quitPending, busy, modalOpen, syncNotice;
    public DateTimeOffset LastInteraction { get; private set; } = DateTimeOffset.UtcNow;
    public MainWindow(bool demo, bool render)
    {
        this.demo = demo; this.render = render; preferences = demo ? new() : UserSettings.Load();
        vm = new(demo); DataContext = vm; InitializeComponent();
        if (!render)
        {
            var area = SystemParameters.WorkArea;
            MinWidth = Math.Min(MinWidth, Math.Max(640, area.Width - 32));
            MinHeight = Math.Min(MinHeight, Math.Max(480, area.Height - 32));
            Width = Math.Min(Width, area.Width - 32); Height = Math.Min(Height, area.Height - 32);
        }
        SizeChanged += (_, _) =>
        {
            bool compact = ActualHeight < 660;
            OverviewPage.RowDefinitions[0].Height = new GridLength(compact ? 108 : 112);
            OverviewPage.RowDefinitions[1].Height = new GridLength(compact ? 120 : 148);
            OverviewPage.RowDefinitions[2].Height = new GridLength(compact ? 44 : 56);
        };
        SetLanguage(preferences.Language == "en"); ApplyTheme(preferences.Theme);
        DeviceNameInput.Text = vm.DeviceName; ThemeChoice.SelectedIndex = preferences.Theme == "light" ? 1 : preferences.Theme == "system" ? 2 : 0;
        LanguageChoice.SelectedIndex = preferences.Language == "en" ? 1 : 0; AutostartChoice.IsChecked = preferences.Autostart;
        Closing += OnClosing;
        PreviewMouseDown += (_, _) => LastInteraction = DateTimeOffset.UtcNow;
        PreviewKeyDown += (_, _) => LastInteraction = DateTimeOffset.UtcNow;
        if (demo) { vm.DeviceName = vm.Text("ThisPC"); vm.NotifyDevice(); DeviceNameInput.Text = vm.DeviceName; InitializeSettingsDraft(); return; }
        Directory.CreateDirectory(UserSettings.DataDirectory);
        service = new(Path.Combine(UserSettings.DataDirectory, "companion.sqlite"));
        vm.DeviceName = service.GetConfiguration().DeviceName; vm.NotifyDevice(); DeviceNameInput.Text = vm.DeviceName;
        using (var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Hourstone.ico"))!.Stream)
        using (var icon = new System.Drawing.Icon(iconStream))
            tray = new Forms.NotifyIcon { Icon = (System.Drawing.Icon)icon.Clone(), Text = "Hourstone Companion", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Hourstone Companion", null, (_, _) => Dispatcher.Invoke(ShowMain));
        menu.Items.Add(vm.Text("CheckNow"), null, (_, _) => Dispatcher.InvokeAsync(async () => await ScanAsync()));
        menu.Items.Add(vm.English ? "Quit" : "Beenden", null, (_, _) => Dispatcher.Invoke(Quit)); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMain);
        showSignal = new(false, EventResetMode.AutoReset, "Local\\HourstoneCompanion.Show");
        showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => Dispatcher.InvokeAsync(ShowMain), null, Timeout.Infinite, false);
        updater = new(this, Notify);
        AutostartChoice.IsEnabled = updater.SupportsAutostart;
        AutostartChoice.ToolTip = vm.English ? "Available after installation." : "Nach Installation verfügbar.";
        if (updater.SupportsAutostart) ApplyAutostart(preferences.Autostart);
        InitializeSettingsDraft();
        RefreshSources(); RefreshCloud(); RebuildWatchers();
        timer.Tick += async (_, _) =>
        {
            if (busy) return;
            if (DateTimeOffset.UtcNow - lastScan >= TimeSpan.FromSeconds(30) || DateTimeOffset.UtcNow - changed >= TimeSpan.FromMilliseconds(750))
                await ScanAsync();
            if (updater != null) await updater.TickAsync();
        };
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        timer.Start(); Dispatcher.InvokeAsync(async () => await ScanAsync());
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (!render) windowWorkArea = new WindowWorkArea(this);
    }
    public void SetLanguage(bool english)
    {
        vm.SetLanguage(english);
        if (demo && settingsDraft == null) { vm.DeviceName = vm.Text("ThisPC"); vm.NotifyDevice(); DeviceNameInput.Text = vm.DeviceName; }
        ((ComboBoxItem)ThemeChoice.Items[0]).Content = english ? "Dark" : "Dunkel";
        ((ComboBoxItem)ThemeChoice.Items[1]).Content = english ? "Light" : "Hell";
        ((ComboBoxItem)ThemeChoice.Items[2]).Content = english ? "Windows setting" : "Windows-Einstellung";
        RefreshSettingsState();
        if (tray?.ContextMenuStrip is { } menu) { menu.Items[1].Text = vm.Text("CheckNow"); menu.Items[2].Text = english ? "Quit" : "Beenden"; }
        if (CharacterGrid != null) { CharacterGrid.Columns[0].Header = vm.Text("CharacterHeader"); CharacterGrid.Columns[2].Header = vm.Text("TimeHeader"); CharacterGrid.Columns[3].Header = vm.Text("UpdatedHeader"); }
        if (AutostartChoice != null) AutostartChoice.ToolTip = english ? "Available after installation." : "Nach Installation verfügbar.";
        if (UpdateText != null) UpdateText.Text = UpdateCoordinator.LocalizeStatus(UpdateText.Text, english);
        RefreshSources(); RefreshCloud();
        if (service != null && syncNotice && displayedSyncResult is { } displayedResult)
        {
            DiagnosticsText.Text = SourceStatusPresentation.Diagnostics(displayedResult, service.GetConfiguration().Sources, english);
            UpdateSyncNotice(displayedResult);
        }
    }
    void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (preferences.Theme == "system" && !Dispatcher.HasShutdownStarted) Dispatcher.InvokeAsync(() => ApplyTheme("system"));
    }
    public void ApplyTheme(string theme)
    {
        bool light = theme == "light";
        if (theme == "system") { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); light = key?.GetValue("AppsUseLightTheme") is int n && n == 1; }
#pragma warning disable WPF0001
        Application.Current.ThemeMode = light ? ThemeMode.Light : ThemeMode.Dark;
#pragma warning restore WPF0001
        var colors = light ? new Dictionary<string, string> { { "Surface", "#F1F5F9" }, { "Panel", "#FFFFFF" }, { "Sidebar", "#E7EFF5" }, { "Line", "#C4D2DF" }, { "Text", "#182C3C" }, { "Muted", "#526C82" }, { "Gold", "#86610B" }, { "Selection", "#D2EAF5" } } : new Dictionary<string, string> { { "Surface", "#131E29" }, { "Panel", "#182633" }, { "Sidebar", "#152330" }, { "Line", "#304555" }, { "Text", "#ECF3FF" }, { "Muted", "#A6BFD8" }, { "Gold", "#F5CD66" }, { "Selection", "#203F54" } };
        colors["CaptionHover"] = light ? "#0F000000" : "#1AFFFFFF";
        colors["CaptionPressed"] = light ? "#0A000000" : "#0FFFFFFF";
        colors["Error"] = light ? "#B42318" : "#F17D72";
        vm.SetLight(light);
        foreach (var color in colors) Application.Current.Resources[color.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Value));
        RefreshSettingsState(); RefreshSources(); RefreshCloud();
    }
    void Navigate(object sender, RoutedEventArgs e)
    {
        if (OverviewPage == null) return;
        var page = (string)((RadioButton)sender).Tag;
        OverviewPage.Visibility = page == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        ClientsPage.Visibility = page == "Clients" ? Visibility.Visible : Visibility.Collapsed;
        SyncPage.Visibility = page == "Sync" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        LastInteraction = DateTimeOffset.UtcNow;
        if (page == "Clients") RefreshSources(); if (page == "Sync") RefreshCloud();
    }
    void ShowMain() { Show(); WindowState = WindowState.Normal; Activate(); }
    void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
    void CaptionButton_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not Button button) return;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true;
    }
    void OnClosing(object? sender, CancelEventArgs e) { if (!quit) { e.Cancel = true; if (demo) Quit(); else Hide(); } }
    public void Quit()
    {
        if (busy) { quitPending = true; stopping.Cancel(); Hide(); return; }
        quit = true; SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged; timer.Stop(); stopping.Cancel(); foreach (var w in watchers) w.Dispose(); watchers.Clear();
        showWait?.Unregister(null); showSignal?.Dispose(); tray?.Dispose(); service?.Dispose(); stopping.Dispose();
        windowWorkArea?.Dispose(); windowWorkArea = null;
        Application.Current.Shutdown();
    }
    void Notify(string text)
    {
        syncNotice = false; NoticeSourceButton.Visibility = Visibility.Collapsed; NoticeText.Text = text; Notice.Visibility = Visibility.Visible;
        if (!IsVisible && tray != null) { tray.BalloonTipTitle = "Hourstone Companion"; tray.BalloonTipText = text; tray.ShowBalloonTip(6000); }
    }
    void UpdateSyncNotice(SyncResult result)
    {
        displayedSyncResult = result;
        var announce = syncNotifications.Update(result.Issues);
        if (result.Success)
        {
            if (syncNotice) { Notice.Visibility = Visibility.Collapsed; NoticeSourceButton.Visibility = Visibility.Collapsed; syncNotice = false; }
            return;
        }
        // An unchanged background scan must not replace an unrelated update or settings notice.
        if (!announce && !syncNotice && Notice.Visibility == Visibility.Visible) return;
        var pending = result.LocalSourceStatuses.Count(status => status.Readiness != LocalSourceReadiness.Ready);
        var text = pending > 0
            ? (vm.English ? $"{pending} account source(s) need attention. Open Clients for the next step. Last valid data is kept." : $"{pending} Account-Quelle(n) benötigen Aufmerksamkeit. Unter Clients findest du den nächsten Schritt. Gültige Daten bleiben erhalten.")
            : (vm.English ? "Sync needs attention. Last valid data is kept. See local diagnostics in Settings." : "Der Abgleich benötigt Aufmerksamkeit. Gültige Daten bleiben erhalten. Details stehen in der lokalen Diagnose unter Einstellungen.");
        NoticeText.Text = text; Notice.Visibility = Visibility.Visible;
        NoticeSourceButton.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed; syncNotice = true;
        if (announce && !IsVisible && tray != null)
        {
            tray.BalloonTipTitle = "Hourstone Companion"; tray.BalloonTipText = text; tray.ShowBalloonTip(6000);
        }
    }
    void ShowSources_Click(object sender, RoutedEventArgs e)
    {
        ClientsNav.IsChecked = true; RefreshSources();
        var first = service?.LastResult.LocalSourceStatuses.FirstOrDefault(status => status.Readiness != LocalSourceReadiness.Ready);
        if (first is not null)
            Dispatcher.InvokeAsync(() => SourcesPanel.Children.OfType<FrameworkElement>().FirstOrDefault(card => Equals(card.Tag, first.SourceId))?.BringIntoView(), DispatcherPriority.Loaded);
    }
    async Task ScanAsync()
    {
        if (service == null || busy) return; busy = true; vm.IsIdle = false; vm.Status = vm.Text("Busy");
        try
        {
            var result = await service.SyncNowAsync(stopping.Token);
            vm.RecordCheck(result.CompletedAt);
            vm.SetObservations(service.GetCharacters(), service.GetRemovedCharacters());
            vm.Status = vm.Text(result.Success ? (result.SourceCount > 0 ? "Active" : "Unconfigured") : "Attention");
            DiagnosticsText.Text = SourceStatusPresentation.Diagnostics(result, service.GetConfiguration().Sources, vm.English);
            UpdateSyncNotice(result);
            if (ClientsPage.IsVisible) RefreshSources();
            RefreshCloud();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        { vm.Status = vm.Text("Attention"); DiagnosticsText.Text = ex.Message; UpdateSyncNotice(SyncResult.Empty with { Issues = [new SyncIssue("sync_failed", ex.Message)] }); }
        finally { lastScan = DateTimeOffset.UtcNow; changed = DateTimeOffset.MaxValue; busy = false; vm.IsIdle = true; if (quitPending) Quit(); }
    }
    async void SyncNow_Click(object sender, RoutedEventArgs e) { if (demo) Notify(vm.English ? "Preview with sample data." : "Vorschau mit Beispieldaten."); else await ScanAsync(); }
    void ToggleRemoved_Click(object sender, RoutedEventArgs e) => vm.ShowRemoved = !vm.ShowRemoved;
    void OpenProductLink_Click(object sender, RoutedEventArgs e)
    {
        if (render || sender is not FrameworkElement { Tag: ProductLink link }) return;
        var result = ProductLinks.Open(link);
        if (!result.Succeeded)
        {
            var message = string.Format(vm.Culture, vm.Text("BrowserOpenFailed"), ProductLinks.Address(link));
            Notify(message); DiagnosticsText.Text = message + Environment.NewLine + result.TechnicalError;
        }
    }
    async void CharacterAction_Click(object sender, RoutedEventArgs e)
    {
        if (!vm.CanChangeCharacter || vm.SelectedRow is not { } row) return;
        bool remove = !vm.ShowRemoved;
        if (remove)
        {
            modalOpen = true;
            try
            {
                if (MessageBox.Show(this, string.Format(vm.Culture, vm.Text("RemoveConfirmation"), row.Name + " · " + row.Client + " · " + row.Value.Realm),
                    vm.Text("RemoveCharacter"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            }
            finally { modalOpen = false; LastInteraction = DateTimeOffset.UtcNow; }
        }
        try
        {
            if (demo)
            {
                var visible = vm.VisibleObservations.ToList(); var removed = vm.RemovedObservations.ToList();
                (remove ? visible : removed).RemoveAll(o => ObservationRules.Identity(o) == ObservationRules.Identity(row.Value));
                (remove ? removed : visible).Add(row.Value); vm.SetObservations(visible, removed);
            }
            else if (service != null)
            {
                service.SetCharacterRemoved(row.Value, remove);
                vm.SetObservations(service.GetCharacters(), service.GetRemovedCharacters());
                await ScanAsync();
            }
        }
        catch (Exception ex) when (IsSettingsPersistenceError(ex) || ex is InvalidDataException or System.Text.Json.JsonException)
        { Notify(vm.Text("VisibilityFailed")); DiagnosticsText.Text = ex.Message; }
    }
    public void SetRemovedPreview()
    {
        if (!demo) return;
        var all = MainViewModel.DemoData(); vm.SetObservations(all.Skip(2), all.Take(2)); vm.ShowRemoved = true;
        vm.SelectedRow = vm.Rows.FirstOrDefault();
    }
    void Hours_Click(object sender, RoutedEventArgs e) => vm.SetHours(true);
    void Days_Click(object sender, RoutedEventArgs e) => vm.SetHours(false);
    void RebuildWatchers()
    {
        foreach (var w in watchers) w.Dispose(); watchers.Clear(); if (service == null) return;
        var config = service.GetConfiguration();
        var paths = config.Sources.Where(x => x.Enabled).Select(x => Path.GetDirectoryName(x.SavedVariablesPath)!).ToList();
        if (config.CloudFolder != null && !config.CloudPaused) paths.Add(config.CloudFolder);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
        {
            try
            {
                var watcher = new FileSystemWatcher(path) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
                FileSystemEventHandler handler = (_, _) => Dispatcher.InvokeAsync(() => changed = DateTimeOffset.UtcNow);
                watcher.Changed += handler; watcher.Created += handler; watcher.Deleted += handler; watcher.Renamed += (_, _) => Dispatcher.InvokeAsync(() => changed = DateTimeOffset.UtcNow);
                watcher.Error += (_, _) => Dispatcher.InvokeAsync(() => changed = DateTimeOffset.UtcNow); watcher.EnableRaisingEvents = true; watchers.Add(watcher);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    async void AddWoW_Click(object sender, RoutedEventArgs e)
    {
        if (demo || service == null || busy) return;
        var picker = new OpenFolderDialog { Title = vm.Text("AddWoW"), Multiselect = false };
        if (ShowFolderDialog(picker) == true) await AddSourcesAsync([picker.FolderName]);
    }
    async void Discover_Click(object sender, RoutedEventArgs e)
    {
        if (demo || service == null || busy) return;
        var roots = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "World of Warcraft"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "World of Warcraft") };
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (var key = machine.OpenSubKey(@"SOFTWARE\Blizzard Entertainment\World of Warcraft"))
                if (key?.GetValue("InstallPath") is string path) roots.Add(path);
        foreach (var drive in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
            foreach (var folder in new[] { "World of Warcraft", "Games\\World of Warcraft", "Blizzard\\World of Warcraft" })
                roots.Add(Path.Combine(drive.RootDirectory.FullName, folder));
        await AddSourcesAsync(roots.Where(Directory.Exists));
    }
    async Task AddSourcesAsync(IEnumerable<string> roots)
    {
        if (service == null) return;
        try
        {
            var found = service.DiscoverSources(roots); var config = service.GetConfiguration(); var sources = config.Sources.ToList();
            foreach (var source in found) if (!sources.Any(x => string.Equals(x.SavedVariablesPath, source.SavedVariablesPath, StringComparison.OrdinalIgnoreCase))) sources.Add(source.Configuration);
            if (found.Count == 0) { Notify(vm.Text("NoSources") + " " + vm.Text("FirstSave")); return; }
            service.SaveConfiguration(config with { Sources = sources }); Notice.Visibility = Visibility.Collapsed; RefreshSources(); RebuildWatchers();
            await ScanAsync();
            if (service.LastResult.Success) Notify(vm.English ? "Sources added. Restart WoW once to load the new data addon. Later updates are loaded on login or /reload." : "Quellen hinzugefügt. Starte WoW einmal vollständig neu, damit das neue Datenaddon erkannt wird. Spätere Aktualisierungen werden beim Login oder /reload geladen.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { Notify(ex.Message); }
    }
    void RefreshSources()
    {
        if (SourcesPanel == null) return; SourcesPanel.Children.Clear();
        IReadOnlyList<SourceConfiguration> sources = sourcePreview ?? service?.GetConfiguration().Sources ?? [];
        IReadOnlyList<LocalSourceStatus> statuses = sourceStatusPreview ?? service?.LastResult.LocalSourceStatuses ?? [];
        if (sources.Count == 0) { SourcesPanel.Children.Add(new TextBlock { Text = vm.Text("NoSources") }); return; }
        foreach (var source in sources)
        {
            var panel = new StackPanel();
            var checkbox = new CheckBox { Content = MainViewModel.ClientName(source.Flavor) + " · " + source.AccountName, IsChecked = source.Enabled };
            checkbox.Click += async (_, _) =>
            {
                var configuredService = service;
                if (busy || configuredService == null) { checkbox.IsChecked = source.Enabled; return; }
                try { var c = configuredService.GetConfiguration(); configuredService.SaveConfiguration(c with { Sources = c.Sources.Select(x => x.SavedVariablesPath == source.SavedVariablesPath ? x with { Enabled = checkbox.IsChecked == true } : x).ToList() }); RebuildWatchers(); await ScanAsync(); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException) { Notify(ex.Message); }
            };
            panel.Children.Add(checkbox);
            panel.Children.Add(new TextBlock { Text = source.ClientDirectory, FontSize = 13, Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = "Region: " + source.Region, FontSize = 13, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 10, 0, 0) });
            var status = statuses.FirstOrDefault(item => item.SourceId == source.SourceId);
            if (source.Enabled && status != null)
            {
                var title = new TextBlock { Text = SourceStatusPresentation.Title(status, vm.English), FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 5) };
                title.SetResourceReference(TextBlock.ForegroundProperty, status.Readiness == LocalSourceReadiness.Ready ? "Text" : "Gold");
                panel.Children.Add(title);
                var instruction = new TextBlock { Text = SourceStatusPresentation.Instruction(status, vm.English), FontSize = 14, TextWrapping = TextWrapping.Wrap };
                instruction.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(instruction);
                if (ProductLinks.AddonActionKey(status.Readiness) is { } actionKey)
                {
                    var action = new Button
                    {
                        Content = vm.Text(actionKey), Style = (Style)FindResource("PrimaryButton"), Tag = ProductLink.AddonCurseForge,
                        HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 3), Padding = new Thickness(14, 8, 14, 8),
                        ToolTip = vm.Text("AddonDownloadTooltip")
                    };
                    System.Windows.Automation.AutomationProperties.SetAutomationId(action, "AddonDownload-" + source.SourceId);
                    System.Windows.Automation.AutomationProperties.SetName(action, vm.Text(actionKey) + " · " + MainViewModel.ClientName(source.Flavor) + " · " + source.AccountName);
                    System.Windows.Automation.AutomationProperties.SetHelpText(action, vm.Text("ExternalBrowserHint"));
                    action.Click += OpenProductLink_Click; panel.Children.Add(action);
                }
            }
            else panel.Children.Add(new TextBlock { Text = source.Enabled ? (vm.English ? "Waiting for the first check …" : "Warte auf die erste Prüfung …") : (vm.English ? "Not selected for synchronization" : "Nicht für den Abgleich ausgewählt"), FontSize = 14, Margin = new Thickness(0, 12, 0, 0) });
            var border = new Border { Style = (Style)FindResource("Card"), Child = panel, Tag = source.SourceId, Margin = new Thickness(0, 0, 0, 12) };
            if (source.Enabled && status is not null && status.Readiness != LocalSourceReadiness.Ready) border.SetResourceReference(Border.BorderBrushProperty, "Gold");
            SourcesPanel.Children.Add(border);
        }
    }
    public void SetClientsPreview(string state)
    {
        if (!demo) throw new InvalidOperationException("Synthetic client previews are only available in preview mode.");
        if (state is not ("empty" or "missing" or "outdated")) throw new ArgumentException("Unknown client preview state.", nameof(state));
        var source = new SourceConfiguration
        {
            SourceId = "synthetic-preview", WoWRoot = @"C:\Synthetic WoW", ClientDirectory = @"C:\Synthetic WoW\_retail_",
            AccountName = "SYNTHETIC_ACCOUNT", Region = "eu", Flavor = "retail"
        };
        sourcePreview = state == "empty" ? [] : [source];
        sourceStatusPreview = state == "empty" ? [] : [new LocalSourceStatus(source.SourceId, source.ClientDirectory, source.AccountName, source.Flavor,
            state == "missing" ? LocalSourceReadiness.AddonMissing : LocalSourceReadiness.AddonOutdated, state == "missing" ? null : "0.2.1")];
        RefreshSources();
    }
    void RefreshCloud()
    {
        if (FolderText == null) return;
        if (demo && syncPreviewState != null) { RefreshSyncPreview(); return; }
        var config = service?.GetConfiguration(); FolderText.Text = config?.CloudFolder ?? vm.Text("NoneFolder"); PauseButton.Content = vm.Text(config?.CloudPaused == true ? "Resume" : "Pause");
        PauseButton.IsEnabled = DetachButton.IsEnabled = config?.CloudFolder != null;
        var result = service?.LastResult;
        PublicationText.Text = config?.CloudFolder == null ? vm.Text("CloudDisconnected") :
            config.CloudPaused ? vm.Text("CloudPaused") :
            result?.CloudPublished == true ? vm.CloudAvailability(result.CompletedAt) :
            result?.Issues.Any(issue => issue.Code == "cloud_unavailable") == true ? vm.Text("CloudFailed") : vm.Text("CloudPending");
        WoWStatusText.Text = vm.Text(result?.AddonReady == true ? "WoWOverviewSaved" :
            config?.Sources.Any(source => source.Enabled) != true ? "WoWOverviewUnconfigured" :
            result == null || result.CompletedAt == DateTimeOffset.MinValue ? "WoWOverviewUnchecked" : "WoWOverviewPending");
        DevicesPanel.Children.Clear(); var devices = service?.GetDevices();
        if (devices == null || devices.Count == 0) { DevicesPanel.Children.Add(new TextBlock { Text = vm.Text("NoDevices"), TextWrapping = TextWrapping.Wrap }); return; }
        foreach (var device in devices)
        {
            var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = device.DeviceName, FontSize = 19, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = $"{device.CharacterCount} {vm.Text("Characters")} · " + (device.IsLocal ? (vm.English ? "This device" : "Dieses Gerät") : (vm.English ? "Received: " : "Empfangen: ") + device.LastSeen.ToLocalTime().ToString("g", vm.Culture)), Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 8, 0, 0) });
            DevicesPanel.Children.Add(new Border { Style = (Style)FindResource("Card"), Child = panel, Margin = new Thickness(0, 0, 0, 12) });
        }
    }
    public void SetSyncPreview(string state)
    {
        if (!demo) return;
        if (state is not ("disconnected" or "connected" or "paused" or "error" or "unchanged")) throw new ArgumentException("Unknown synchronization preview state.", nameof(state));
        syncPreviewState = state; SetRenderPage("sync"); RefreshCloud();
    }
    void RefreshSyncPreview()
    {
        var disconnected = syncPreviewState == "disconnected";
        var completedAt = new DateTimeOffset(DateTime.Today.AddHours(14).AddMinutes(syncPreviewState == "unchanged" ? 33 : 32));
        FolderText.Text = disconnected ? vm.Text("NoneFolder") : @"C:\Synthetic Dropbox\HourstoneSync";
        PauseButton.Content = vm.Text(syncPreviewState == "paused" ? "Resume" : "Pause");
        PauseButton.IsEnabled = DetachButton.IsEnabled = !disconnected;
        PublicationText.Text = disconnected ? vm.Text("CloudDisconnected") : syncPreviewState switch
        {
            "paused" => vm.Text("CloudPaused"),
            "error" => vm.Text("CloudFailed"),
            _ => vm.CloudAvailability(completedAt)
        };
        WoWStatusText.Text = vm.Text("WoWOverviewSaved");
        vm.Status = vm.Text(syncPreviewState == "error" ? "Attention" : "Active");
        vm.RecordCheck(completedAt);
        DevicesPanel.Children.Clear();
        if (disconnected) { DevicesPanel.Children.Add(new TextBlock { Text = vm.Text("NoDevices"), TextWrapping = TextWrapping.Wrap }); return; }
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Sample laptop", FontSize = 19, FontWeight = FontWeights.SemiBold });
        var receipt = vm.English ? "Received from this device" : "Von diesem Gerät empfangen";
        panel.Children.Add(new TextBlock { Text = $"4 {vm.Text("Characters")} · {receipt}: 14:30", Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        DevicesPanel.Children.Add(new Border { Style = (Style)FindResource("Card"), Child = panel, Margin = new Thickness(0, 0, 0, 12) });
    }
    async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (demo || service == null || busy) return;
        var picker = new OpenFolderDialog { Title = vm.Text("ChooseFolder") }; if (ShowFolderDialog(picker) != true) return;
        try { await service.JoinSyncFolderAsync(picker.FolderName, stopping.Token); RebuildWatchers(); RefreshCloud(); await ScanAsync(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { Notify(ex.Message); }
    }
    async void Pause_Click(object sender, RoutedEventArgs e) { if (service == null || busy) return; service.PauseCloud(!service.GetConfiguration().CloudPaused); RebuildWatchers(); RefreshCloud(); await ScanAsync(); }
    async void Detach_Click(object sender, RoutedEventArgs e) { if (service == null || busy) return; service.DetachSyncFolder(); RebuildWatchers(); RefreshCloud(); await ScanAsync(); }
    void InitializeSettingsDraft()
    {
        settingsDraft = new(new(vm.DeviceName, preferences.Theme, preferences.Language, preferences.Autostart));
        DeviceNameInput.TextChanged += SettingsValueChanged;
        ThemeChoice.SelectionChanged += SettingsValueChanged;
        LanguageChoice.SelectionChanged += SettingsValueChanged;
        AutostartChoice.Checked += SettingsValueChanged;
        AutostartChoice.Unchecked += SettingsValueChanged;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.IsIdle)) RefreshSettingsState(); };
        RefreshSettingsState();
    }
    void SettingsValueChanged(object sender, RoutedEventArgs e)
    {
        if (settingsDraft == null) return;
        settingsDraft.Update(new(DeviceNameInput.Text, ThemeChoice.SelectedIndex == 1 ? "light" : ThemeChoice.SelectedIndex == 2 ? "system" : "dark", LanguageChoice.SelectedIndex == 1 ? "en" : "de", AutostartChoice.IsChecked == true));
        RefreshSettingsState();
    }
    void RefreshSettingsState()
    {
        if (settingsDraft == null || SaveSettingsButton == null) return;
        SaveSettingsButton.IsEnabled = settingsDraft.CanSave(busy || savingSettings);
        string? key = settingsDraft.Feedback == SettingsFeedbackState.Failed ? "SettingsSaveFailed"
            : !settingsDraft.IsValid ? "SettingsInvalidName"
            : settingsDraft.Feedback == SettingsFeedbackState.Saved ? "SettingsSaved"
            : settingsDraft.IsDirty ? "SettingsUnsaved" : null;
        SettingsFeedback.Visibility = key == null ? Visibility.Collapsed : Visibility.Visible;
        SettingsFeedback.Text = key == null ? "" : vm.Text(key);
        SettingsFeedback.ToolTip = settingsDraft.FailureDetail;
        SettingsFeedback.Foreground = (Brush)FindResource(settingsDraft.Feedback == SettingsFeedbackState.Failed || !settingsDraft.IsValid ? "Error" : settingsDraft.IsDirty ? "Muted" : "Cyan");
    }
    async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (settingsDraft?.CanSave(busy || savingSettings) != true) return;
        savingSettings = true; RefreshSettingsState();
        bool saved = false;
        try
        {
            saved = settingsDraft.Save(PersistSettings);
            if (!saved) return;
            var values = settingsDraft.Saved;
            preferences = values.ToPreferences(); vm.DeviceName = values.DeviceName; vm.NotifyDevice();
            DeviceNameInput.Text = values.DeviceName;
            SetLanguage(preferences.Language == "en"); ApplyTheme(preferences.Theme);
            RefreshSources(); RefreshCloud();
        }
        catch (Exception ex) when (IsSettingsPersistenceError(ex)) { }
        finally { savingSettings = false; RefreshSettingsState(); }
        if (saved) await ScanAsync();
    }
    void PersistSettings(SettingsValues values)
    {
        if (demo) return;
        if (service == null) throw new InvalidOperationException("Local settings are unavailable.");
        var previous = service.GetConfiguration(); var next = values.ToPreferences();
        try
        {
            next.Save(); ApplyAutostart(next.Autostart);
            service.SaveConfiguration(previous with { DeviceName = values.DeviceName });
        }
        catch (Exception ex) when (IsSettingsPersistenceError(ex))
        {
            var errors = new List<Exception> { ex };
            try { preferences.Save(); } catch (Exception rollback) when (IsSettingsPersistenceError(rollback)) { errors.Add(rollback); }
            try { ApplyAutostart(preferences.Autostart); } catch (Exception rollback) when (IsSettingsPersistenceError(rollback)) { errors.Add(rollback); }
            try { service.SaveConfiguration(previous); } catch (Exception rollback) when (IsSettingsPersistenceError(rollback)) { errors.Add(rollback); }
            if (errors.Count > 1) throw new AggregateException("Settings could not be saved or fully restored. The draft is kept; retry saving.", errors);
            throw;
        }
    }
    static bool IsSettingsPersistenceError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or DbException or SecurityException or AggregateException;
    void ApplyAutostart(bool enabled)
    {
        if (updater?.SupportsAutostart != true) return;
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        var stub = Path.Combine(updater.InstallDirectory ?? throw new IOException("Installation directory unavailable."), "Hourstone.Companion.exe");
        if (!File.Exists(stub)) throw new IOException("Installed startup launcher is missing.");
        if (enabled) run.SetValue("HourstoneCompanion", "\"" + stub + "\" --background"); else run.DeleteValue("HourstoneCompanion", false);
    }
    async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (updater == null) { UpdateText.Text = UpdateCoordinator.StatusMessage("NotInstalled", vm.English); return; }
        UpdateText.Text = await updater.CheckAsync(true);
    }
    bool? ShowFolderDialog(OpenFolderDialog picker)
    {
        modalOpen = true;
        try { return picker.ShowDialog(this); }
        finally { modalOpen = false; LastInteraction = DateTimeOffset.UtcNow; }
    }
    public void SetRenderPage(string page)
    {
        if (!demo) return;
        (page switch { "clients" => ClientsNav, "sync" => SyncNav, "settings" => SettingsNav, _ => OverviewNav }).IsChecked = true;
    }
    public void SetRenderTheme(string theme)
    {
        if (!demo) return;
        if (theme is not ("dark" or "light" or "system")) throw new ArgumentException("Unknown preview theme.", nameof(theme));
        ResetDemoPreferences(preferences with { Theme = theme });
    }
    public void SetRenderLanguage(bool english)
    {
        if (demo) ResetDemoPreferences(preferences with { Language = english ? "en" : "de" });
    }
    void ResetDemoPreferences(UserSettings values)
    {
        settingsDraft = null; preferences = values;
        SetLanguage(preferences.Language == "en"); ApplyTheme(preferences.Theme);
        DeviceNameInput.Text = vm.DeviceName;
        ThemeChoice.SelectedIndex = preferences.Theme == "light" ? 1 : preferences.Theme == "system" ? 2 : 0;
        LanguageChoice.SelectedIndex = preferences.Language == "en" ? 1 : 0;
        AutostartChoice.IsChecked = preferences.Autostart;
        settingsDraft = new(new(vm.DeviceName, preferences.Theme, preferences.Language, preferences.Autostart));
        RefreshSettingsState();
    }
    public void SetSettingsDraftPreview()
    {
        if (!demo) return;
        DeviceNameInput.Text = "Azeroth Laptop"; ThemeChoice.SelectedIndex = ThemeChoice.SelectedIndex == 1 ? 0 : 1;
    }
    public void SetLongNamePreview()
    {
        if (demo) vm.SetObservations(MainViewModel.DemoData().Select((o, i) => i == 0 ? o with { Name = new string('W', 64), Realm = "A very long realm name for layout validation", Guild = "A very long synthetic guild name for layout validation", GuildUpdatedAt = o.UpdatedAt } : o));
    }
    public bool English => vm.English;
    public bool CanApplyUpdate(DateTimeOffset noticeAt, bool wowRunning) => settingsDraft?.IsDirty != true && !savingSettings && UpdatePolicy.CanApply(busy, IsVisible, IsActive, modalOpen, LastInteraction, noticeAt, DateTimeOffset.UtcNow, wowRunning);
    public void PrepareUpdate() { timer.Stop(); }
    public void ResumeAfterUpdateFailure() => timer.Start();
}
