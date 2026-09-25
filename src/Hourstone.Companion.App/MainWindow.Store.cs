using System;
using System.Threading.Tasks;
using System.Windows;

namespace Hourstone.Companion.App;

public partial class MainWindow
{
    StoreSetupViewModel? storeSetup;
    StoreImportCandidate? importCandidate;
    bool storeSettingsPreview;
    AppStartupState startupState;
    bool StoreSettingsVisible => AppDistribution.Current.IsStore || storeSettingsPreview;
    void InitializeStoreSetup()
    {
        importCandidate = StoreImportService.Discover();
        storeSetup = new() { SourceCount = importCandidate.SourceCount, StartWithWindows = preferences.Autostart, Fresh = !importCandidate.Available, RequiresLegacyHandover = StartupService.NeedsLegacyHandover(importCandidate) };
        StoreSetupPage.DataContext = storeSetup;
        StoreSetupPage.PrimaryRequested += StoreSetupPrimary;
        StoreSetupPage.SecondaryRequested += StoreSetupSecondary;
        StoreSetupPage.StartupSettingsRequested += (_, _) => WindowsStartupSettings_Click(StoreSetupPage, new RoutedEventArgs());
        ShowStoreSetup(!AppRuntime.OwnsInstance ? "blocked" : importCandidate.Available ? "found" : "fresh");
        if (importCandidate.Error is { } error) { storeSetup.FailureDetail = error; ShowStoreSetup("error"); }
    }
    void ShowStoreSetup(string stage)
    {
        if (storeSetup is null) return;
        OverviewNav.IsEnabled = ClientsNav.IsEnabled = SyncNav.IsEnabled = SettingsNav.IsEnabled = false;
        OverviewPage.Visibility = ClientsPage.Visibility = SyncPage.Visibility = SettingsPage.Visibility = Visibility.Collapsed;
        StoreSetupPage.Visibility = Visibility.Visible;
        vm.Status = vm.English ? "Synchronization not started" : "Abgleich noch nicht aktiv";
        storeSetup.SetState(stage, vm.English);
    }
    async void StoreSetupPrimary(object? sender, EventArgs args)
    {
        if (storeSetup is not { CanContinue: true } setup) return;
        if (setup.Stage == "done")
        {
            StoreSetupPage.Visibility = Visibility.Collapsed;
            OverviewNav.IsEnabled = ClientsNav.IsEnabled = SyncNav.IsEnabled = SettingsNav.IsEnabled = true;
            if (demo) { OverviewPage.Visibility = Visibility.Visible; vm.ShowProgress = true; return; }
            preferences = UserSettings.Load(); SetLanguage(preferences.Language == "en"); ApplyTheme(preferences.Theme);
            ThemeChoice.SelectedIndex = preferences.Theme == "light" ? 1 : preferences.Theme == "system" ? 2 : 0;
            LanguageChoice.SelectedIndex = preferences.Language == "en" ? 1 : 0; AutostartChoice.IsChecked = preferences.Autostart;
            OverviewPage.Visibility = Visibility.Visible;
            StartNormalRuntime();
            if (setup.Fresh) ClientsNav.IsChecked = true;
            return;
        }
        if (!demo && !AppRuntime.TryAcquireInstance()) { ShowStoreSetup("blocked"); return; }
        if (!demo && !StoreImportService.RequiresSetup)
        {
            StoreSetupPage.Visibility = Visibility.Collapsed;
            OverviewNav.IsEnabled = ClientsNav.IsEnabled = SyncNav.IsEnabled = SettingsNav.IsEnabled = true;
            OverviewPage.Visibility = Visibility.Visible;
            StartNormalRuntime(); return;
        }
        if (setup.Stage is "found" or "blocked" or "error")
        {
            if (setup.Stage == "found") setup.Fresh = false;
            ShowStoreSetup(setup.Fresh ? "fresh" : "prepare"); return;
        }
        ShowStoreSetup("running");
        if (demo) { ShowStoreSetup("done"); return; }
        busy = true;
        try
        {
            var result = setup.Fresh ? await StoreImportService.StartFreshAsync(setup.StartWithWindows)
                : await StoreImportService.ImportAsync(setup.StartWithWindows, cancellationToken: stopping.Token);
            setup.FailureDetail = result.Error ?? "";
            ShowStoreSetup(result.Succeeded ? "done" : "error");
        }
        catch (Exception ex) when (IsSettingsPersistenceError(ex) || ex is OperationCanceledException)
        { setup.FailureDetail = ex.Message; ShowStoreSetup("error"); }
        finally { busy = false; if (quitPending) Quit(); }
    }
    void StoreSetupSecondary(object? sender, EventArgs args)
    {
        if (storeSetup is not { IsIdle: true } setup) return;
        if (setup.Stage == "found") { setup.Fresh = true; ShowStoreSetup("fresh"); }
        else { setup.Fresh = importCandidate?.Available != true; ShowStoreSetup(setup.Fresh ? "fresh" : "found"); }
    }
    void RefreshDistributionLabels()
    {
        if (StoreUpdatesCard is null) return;
        StoreUpdatesCard.Visibility = StoreSettingsVisible ? Visibility.Visible : Visibility.Collapsed;
        DirectUpdatesCard.Visibility = StoreSettingsVisible ? Visibility.Collapsed : Visibility.Visible;
        StoreUpdatesTitle.Text = vm.English ? "Updates from Microsoft Store" : "Updates aus dem Microsoft Store";
        StoreUpdatesDescription.Text = vm.English ? "New versions are available through Microsoft Store. You can also check for updates there." : "Neue Versionen erhältst du über den Microsoft Store. Dort kannst du auch nach Updates suchen.";
        StoreVersionText.Text = (vm.English ? "Installed version: " : "Installierte Version: ") + vm.BuildLabel;
        StoreOpenButton.Content = vm.English ? "Open Microsoft Store" : "Microsoft Store öffnen";
        StoreAddonHint.Text = vm.English ? "Update the WoW addon separately through CurseForge. Progress sync back to the game requires Hourstone 0.3.2." : "Das WoW-Addon aktualisierst du weiterhin über CurseForge. Für den Fortschrittsabgleich zurück ins Spiel benötigst du Hourstone 0.3.2.";
        var disabledInWindows = startupState is AppStartupState.DisabledByUser or AppStartupState.DisabledByPolicy;
        StoreStartupHint.Visibility = StoreSettingsVisible ? Visibility.Visible : Visibility.Collapsed;
        StoreStartupHint.Text = disabledInWindows
            ? vm.English ? "Automatic startup is disabled in Windows. You can manage it in Windows settings." : "Der Autostart ist in Windows ausgeschaltet. Du kannst ihn in den Windows-Einstellungen verwalten."
            : vm.English ? "When enabled, Hourstone starts next to the Windows clock after signing in." : "Wenn aktiviert, startet Hourstone nach der Anmeldung über das Symbol neben der Windows-Uhr.";
        WindowsStartupSettingsButton.Visibility = StoreSettingsVisible && disabledInWindows ? Visibility.Visible : Visibility.Collapsed;
        WindowsStartupSettingsButton.Content = vm.English ? "Open Windows settings" : "Windows-Einstellungen öffnen";
        storeSetup?.SetState(storeSetup.Stage, vm.English);
    }
    async Task RefreshDistributionSettingsAsync()
    {
        RefreshDistributionLabels();
        if (!AppDistribution.Current.IsStore || demo) return;
        loadingStoreSettings = true; RefreshSettingsState();
        try
        {
            startupState = await StartupService.GetStateAsync();
            if (settingsDraft?.IsDirty == true) { RefreshDistributionLabels(); return; }
            settingsDraft = null;
            preferences = preferences with { Autostart = startupState == AppStartupState.Enabled };
            AutostartChoice.IsChecked = preferences.Autostart;
            AutostartChoice.IsEnabled = startupState is AppStartupState.Enabled or AppStartupState.Disabled;
            AutostartChoice.ToolTip = null;
            settingsDraft = new(new(vm.DeviceName, preferences.Theme, preferences.Language, preferences.Autostart));
            RefreshSettingsState(); RefreshDistributionLabels();
        }
        catch (Exception ex) when (IsSettingsPersistenceError(ex)) { Notify(ex.Message); }
        finally { loadingStoreSettings = false; RefreshSettingsState(); }
    }
    void WindowsStartupSettings_Click(object sender, RoutedEventArgs e)
    {
        if (demo) return;
        if (!StartupService.OpenWindowsSettings().Succeeded) Notify(vm.English ? "Windows settings could not be opened." : "Die Windows-Einstellungen konnten nicht geöffnet werden.");
    }
    public void SetStorePreview(string stage)
    {
        if (!demo) throw new InvalidOperationException("Store previews require sample-data mode.");
        storeSettingsPreview = true; RefreshDistributionLabels();
        if (stage == "settings") { SettingsNav.IsChecked = true; return; }
        if (storeSetup is null)
        {
            storeSetup = new() { SourceCount = 2, RequiresLegacyHandover = true }; importCandidate = new(true, "", "AZEROTH-PC", 2);
            StoreSetupPage.DataContext = storeSetup;
            StoreSetupPage.PrimaryRequested += StoreSetupPrimary; StoreSetupPage.SecondaryRequested += StoreSetupSecondary;
        StoreSetupPage.StartupSettingsRequested += (_, _) => WindowsStartupSettings_Click(StoreSetupPage, new RoutedEventArgs());
        }
        if (stage == "error") storeSetup.FailureDetail = vm.English ? "The existing data could not be read. Automatic startup was not changed." : "Auf die bisherigen Daten konnte nicht zugegriffen werden. Der Autostart wurde nicht geändert.";
        ShowStoreSetup(stage);
    }
}
