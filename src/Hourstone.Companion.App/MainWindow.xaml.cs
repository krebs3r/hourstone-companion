using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    UserSettings preferences;
    DateTimeOffset lastScan = DateTimeOffset.MinValue, changed = DateTimeOffset.MaxValue;
    bool quit, quitPending, busy, modalOpen, syncNotice;
    public DateTimeOffset LastInteraction { get; private set; } = DateTimeOffset.UtcNow;
    public MainWindow(bool demo, bool render)
    {
        this.demo = demo; this.render = render; preferences = demo ? new() : UserSettings.Load();
        vm = new(demo); DataContext = vm; InitializeComponent();
        SetLanguage(preferences.Language == "en"); ApplyTheme(preferences.Theme);
        DeviceNameInput.Text = vm.DeviceName; ThemeChoice.SelectedIndex = preferences.Theme == "light" ? 1 : preferences.Theme == "system" ? 2 : 0;
        LanguageChoice.SelectedIndex = preferences.Language == "en" ? 1 : 0; AutostartChoice.IsChecked = preferences.Autostart;
        Closing += OnClosing;
        PreviewMouseDown += (_, _) => LastInteraction = DateTimeOffset.UtcNow;
        PreviewKeyDown += (_, _) => LastInteraction = DateTimeOffset.UtcNow;
        if (demo) { vm.DeviceName = vm.Text("ThisPC"); vm.NotifyDevice(); DeviceNameInput.Text = vm.DeviceName; return; }
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
    public void SetLanguage(bool english)
    {
        vm.SetLanguage(english);
        if (demo) { vm.DeviceName = vm.Text("ThisPC"); vm.NotifyDevice(); DeviceNameInput.Text = vm.DeviceName; }
        LanguageChoice.SelectedIndex = english ? 1 : 0;
        ((ComboBoxItem)ThemeChoice.Items[0]).Content = english ? "Dark" : "Dunkel";
        ((ComboBoxItem)ThemeChoice.Items[1]).Content = english ? "Light" : "Hell";
        if (tray?.ContextMenuStrip is { } menu) { menu.Items[1].Text = vm.Text("CheckNow"); menu.Items[2].Text = english ? "Quit" : "Beenden"; }
        if (CharacterGrid != null) { CharacterGrid.Columns[0].Header = vm.Text("CharacterHeader"); CharacterGrid.Columns[2].Header = vm.Text("TimeHeader"); CharacterGrid.Columns[3].Header = vm.Text("UpdatedHeader"); }
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
        vm.SetLight(light);
        foreach (var color in colors) Application.Current.Resources[color.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Value));
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
    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void Close_Click(object sender, RoutedEventArgs e) { if (demo) Quit(); else Hide(); }
    void OnClosing(object? sender, CancelEventArgs e) { if (!quit) { e.Cancel = true; if (demo) Quit(); else Hide(); } }
    public void Quit()
    {
        if (busy) { quitPending = true; stopping.Cancel(); Hide(); return; }
        quit = true; SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged; timer.Stop(); stopping.Cancel(); foreach (var w in watchers) w.Dispose(); watchers.Clear();
        showWait?.Unregister(null); showSignal?.Dispose(); tray?.Dispose(); service?.Dispose(); stopping.Dispose();
        Application.Current.Shutdown();
    }
    void Notify(string text)
    {
        syncNotice = false; NoticeText.Text = text; Notice.Visibility = Visibility.Visible;
        if (!IsVisible && tray != null) { tray.BalloonTipTitle = "Hourstone Companion"; tray.BalloonTipText = text; tray.ShowBalloonTip(6000); }
    }
    async Task ScanAsync()
    {
        if (service == null || busy) return; busy = true; vm.IsIdle = false; vm.Status = vm.Text("Busy");
        try
        {
            var result = await service.SyncNowAsync(stopping.Token);
            vm.SetObservations(service.GetCharacters());
            vm.Status = vm.Text(result.Success ? (result.SourceCount > 0 ? "Active" : "Unconfigured") : "Attention");
            vm.LastSync = result.AddonReady ? (vm.English ? "Ready for WoW" : "Für WoW bereitgestellt") + " · " + result.CompletedAt.ToLocalTime().ToString("HH:mm") : "";
            DiagnosticsText.Text = result.Success ? (vm.English ? "Last check completed. No errors." : "Letzte Prüfung abgeschlossen. Keine Fehler.") : string.Join(Environment.NewLine, result.Issues.Select(x => x.Code + ": " + x.Message));
            if (!result.Success) Notify(vm.English ? "Some data could not be updated. The last valid data is kept. See local diagnostics in Settings." : "Einige Daten konnten nicht aktualisiert werden. Der letzte gültige Stand bleibt erhalten. Details stehen in den Einstellungen unter Lokale Diagnose.");
            if (result.Success && syncNotice) { Notice.Visibility = Visibility.Collapsed; syncNotice = false; }
            if (!result.Success) syncNotice = true;
            if (ClientsPage.IsVisible) RefreshSources();
            RefreshCloud();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        { vm.Status = vm.Text("Attention"); DiagnosticsText.Text = ex.Message; Notify(vm.English ? "Sync is temporarily unavailable. Your last saved data is kept." : "Der Abgleich ist vorübergehend nicht verfügbar. Dein letzter gespeicherter Stand bleibt erhalten."); }
        finally { lastScan = DateTimeOffset.UtcNow; changed = DateTimeOffset.MaxValue; vm.IsIdle = true; busy = false; if (quitPending) Quit(); }
    }
    async void SyncNow_Click(object sender, RoutedEventArgs e) { if (demo) Notify(vm.English ? "Preview with sample data." : "Vorschau mit Beispieldaten."); else await ScanAsync(); }
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
            Notify(vm.English ? "Sources added. Restart WoW once to load the new data addon. Later updates are loaded on login or /reload." : "Quellen hinzugefügt. Starte WoW einmal vollständig neu, damit das neue Datenaddon erkannt wird. Spätere Aktualisierungen werden beim Login oder /reload geladen.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { Notify(ex.Message); }
    }
    void RefreshSources()
    {
        if (SourcesPanel == null) return; SourcesPanel.Children.Clear();
        if (service == null) { SourcesPanel.Children.Add(new TextBlock { Text = vm.Text("NoSources") }); return; }
        var sources = service.GetConfiguration().Sources;
        if (sources.Count == 0) { SourcesPanel.Children.Add(new TextBlock { Text = vm.Text("NoSources") }); return; }
        foreach (var source in sources)
        {
            var panel = new StackPanel();
            var checkbox = new CheckBox { Content = MainViewModel.ClientName(source.Flavor) + " · " + source.AccountName, IsChecked = source.Enabled };
            checkbox.Click += async (_, _) =>
            {
                if (busy) { checkbox.IsChecked = source.Enabled; return; }
                try { var c = service.GetConfiguration(); service.SaveConfiguration(c with { Sources = c.Sources.Select(x => x.SavedVariablesPath == source.SavedVariablesPath ? x with { Enabled = checkbox.IsChecked == true } : x).ToList() }); RebuildWatchers(); await ScanAsync(); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException) { Notify(ex.Message); }
            };
            panel.Children.Add(checkbox);
            panel.Children.Add(new TextBlock { Text = source.ClientDirectory, FontSize = 13, Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = "Region: " + source.Region, FontSize = 13, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 10, 0, 0) }); var border = new Border { Style = (Style)FindResource("Card"), Child = panel, Margin = new Thickness(0, 0, 0, 12) }; SourcesPanel.Children.Add(border);
        }
    }
    void RefreshCloud()
    {
        if (FolderText == null) return;
        var config = service?.GetConfiguration(); FolderText.Text = config?.CloudFolder ?? vm.Text("NoneFolder"); PauseButton.Content = vm.Text(config?.CloudPaused == true ? "Resume" : "Pause");
        var result = service?.LastResult;
        PublicationText.Text = config?.CloudFolder == null ? "" :
            config.CloudPaused ? (vm.English ? "Folder exchange paused. Received data is kept." : "Ordneraustausch pausiert. Empfangene Daten bleiben erhalten.") :
            result?.CloudPublished == true ? (vm.English ? "Published to sync folder" : "Im Syncordner bereitgestellt") + " · " + result.CompletedAt.ToLocalTime().ToString("HH:mm") :
            (vm.English ? "Folder publication pending. Last valid data is kept." : "Bereitstellung im Syncordner ausstehend. Der letzte gültige Stand bleibt erhalten.");
        WoWStatusText.Text = result?.AddonReady == true ? (vm.English ? "Data ready for WoW; loaded on login or /reload." : "Für WoW bereitgestellt; Übernahme beim Login oder /reload.") : (vm.English ? "No new data ready for WoW yet." : "Noch keine neuen Daten für WoW bereitgestellt.");
        DevicesPanel.Children.Clear(); var devices = service?.GetDevices();
        if (devices == null || devices.Count == 0) { DevicesPanel.Children.Add(new TextBlock { Text = vm.Text("NoDevices"), TextWrapping = TextWrapping.Wrap }); return; }
        foreach (var device in devices)
        {
            var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = device.DeviceName, FontSize = 19, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = $"{device.CharacterCount} {vm.Text("Characters")} · " + (device.IsLocal ? (vm.English ? "This device" : "Dieses Gerät") : (vm.English ? "Received: " : "Empfangen: ") + device.LastSeen.ToLocalTime().ToString("g", vm.Culture)), Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 8, 0, 0) });
            DevicesPanel.Children.Add(new Border { Style = (Style)FindResource("Card"), Child = panel, Margin = new Thickness(0, 0, 0, 12) });
        }
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
    async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (demo || service == null || busy) return;
        var name = DeviceNameInput.Text.Trim(); if (name.Length == 0) { Notify(vm.English ? "Please enter a device name." : "Bitte gib einen Gerätenamen ein."); return; }
        try
        {
            preferences = preferences with { Theme = ThemeChoice.SelectedIndex == 1 ? "light" : ThemeChoice.SelectedIndex == 2 ? "system" : "dark", Language = LanguageChoice.SelectedIndex == 1 ? "en" : "de", Autostart = AutostartChoice.IsChecked == true };
            preferences.Save(); service.SaveConfiguration(service.GetConfiguration() with { DeviceName = name }); vm.DeviceName = name; vm.NotifyDevice();
            SetLanguage(preferences.Language == "en"); ApplyTheme(preferences.Theme); ApplyAutostart(preferences.Autostart); RefreshSources(); RefreshCloud(); await ScanAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { Notify(ex.Message); }
    }
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
        if (updater == null) { UpdateText.Text = vm.English ? "Updates are available in the installed application." : "Updates stehen in der installierten Anwendung zur Verfügung."; return; }
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
    public void SetLongNamePreview()
    {
        if (demo) vm.SetObservations(MainViewModel.DemoData().Select((o, i) => i == 0 ? o with { Name = new string('W', 64), Realm = "A very long realm name for layout validation" } : o));
    }
    public bool English => vm.English;
    public bool CanApplyUpdate(DateTimeOffset noticeAt, bool wowRunning) => UpdatePolicy.CanApply(busy, IsVisible, IsActive, modalOpen, LastInteraction, noticeAt, DateTimeOffset.UtcNow, wowRunning);
    public void PrepareUpdate() { timer.Stop(); }
    public void ResumeAfterUpdateFailure() => timer.Start();
}
