using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Hourstone.Companion.Core;
namespace Hourstone.Companion.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public bool English { get; private set; }
    public CultureInfo Culture => CultureInfo.GetCultureInfo(English ? "en-US" : "de-DE");
    static readonly Dictionary<string, (string De, string En)> Strings = new()
    {
        ["Overview"] = ("Übersicht", "Overview"),
        ["Clients"] = ("Clients", "Clients"),
        ["ShowSources"] = ("Clients ansehen", "View clients"),
        ["Sync"] = ("Synchronisierung", "Synchronization"),
        ["Settings"] = ("Einstellungen", "Settings"),
        ["Local"] = ("Lokal gespeichert", "Stored locally"),
        ["Hero"] = ("Deine Zeit in Azeroth", "Your time in Azeroth"),
        ["Subtitle"] = ("Alle Charaktere. Alle Clients. Ein Überblick.", "Every character. Every client. One overview."),
        ["CheckNow"] = ("Daten abgleichen", "Sync data"),
        ["SavedTime"] = ("Gespeicherte Spielzeit", "Saved playtime"),
        ["Characters"] = ("Charaktere", "Characters"),
        ["RemovedCharacters"] = ("Gelöschte Charaktere", "Deleted characters"),
        ["BackToCharacters"] = ("Zur Charakterliste", "Back to characters"),
        ["RemoveCharacter"] = ("Aus der Übersicht löschen", "Delete from overview"),
        ["RestoreCharacter"] = ("Wiederherstellen", "Restore character"),
        ["RemovedEmpty"] = ("Keine gelöschten Charaktere. Beim Löschen aus der Übersicht bleibt die gespeicherte Spielzeit erhalten.", "No deleted characters. Deleting an entry from the overview keeps its saved playtime."),
        ["RemovalHint"] = ("Aus der Übersicht gelöschte Charaktere zählen nicht zur Gesamtzeit. „Wiederherstellen“ oder ein neuer Login nach dem Empfang der Löschung zeigt sie wieder an. Ein /reload allein genügt dafür nicht.", "Characters deleted from the overview are excluded from totals. Restore them here, or log in again after the addon receives the deletion. A /reload alone does not restore them."),
        ["RemoveConfirmation"] = ("{0} aus der Übersicht löschen?\n\nDer Eintrag zählt danach nicht mehr zur Gesamtspielzeit. Der WoW-Charakter und seine gespeicherte Spielzeit bleiben erhalten.\n\nDu kannst ihn unter „Gelöschte Charaktere“ wiederherstellen. Ein neuer Login stellt ihn ebenfalls wieder her, sobald die Löschung im Addon angekommen ist.", "Delete {0} from the overview?\n\nThe entry will no longer count towards total playtime. The WoW character and its saved playtime are kept.\n\nYou can restore it under Deleted characters. A new login also restores it once the addon has received the deletion."),
        ["VisibilityFailed"] = ("Die Charakterliste konnte nicht geändert werden. Bitte erneut versuchen.", "The character list could not be changed. Please try again."),
        ["Search"] = ("Charakter oder Gilde suchen …", "Search characters or guilds …"),
        ["Hours"] = ("Stunden", "Hours"),
        ["Days"] = ("Tage + Std.", "Days + hours"),
        ["EmptyTitle"] = ("Dein Überblick beginnt hier", "Your overview starts here"),
        ["EmptyText"] = ("Unter Clients findest du das Hourstone-Addon bei CurseForge. Installiere es, logge dich in WoW ein und anschließend aus oder nutze /reload. Füge danach hier deinen WoW-Ordner hinzu.", "Open Clients to find the Hourstone addon on CurseForge. Install it, log in to WoW, then log out or use /reload. Afterwards, add your WoW folder here."),
        ["NoMatches"] = ("Keine Charaktere für diese Filter gefunden.", "No characters match these filters."),
        ["ClientsIntro"] = ("Wähle die lokalen WoW-Installationen und Accounts, deren Spielzeit du zusammen anzeigen möchtest.", "Select the local WoW installations and accounts you want to include."),
        ["AddWoW"] = ("WoW-Ordner hinzufügen", "Add WoW folder"),
        ["Discover"] = ("Installationen suchen", "Find installations"),
        ["AddonSetupTitle"] = ("Das WoW-Addon einrichten", "Set up the WoW addon"),
        ["AddonSetupSteps"] = ($"1. Installiere Hourstone {AddonReadiness.MinimumAddonVersion} oder neuer für deine WoW-Version und aktiviere das Addon.\n2. Logge dich in jedem gewünschten Client ein und anschließend aus oder nutze /reload.\n3. Füge unten deinen WoW-Ordner hinzu oder suche nach Installationen.", $"1. Install Hourstone {AddonReadiness.MinimumAddonVersion} or later for your WoW version and enable the addon.\n2. Log in to each client you want to include, then log out or use /reload.\n3. Add your WoW folder below, or search for installations."),
        ["AddonDownload"] = ("Addon auf CurseForge herunterladen", "Download addon on CurseForge"),
        ["AddonGitHub"] = ("Addon auf GitHub", "Addon on GitHub"),
        ["AddonDownloadTooltip"] = ("Hourstone bei CurseForge im Standardbrowser öffnen", "Open Hourstone on CurseForge in your default browser"),
        ["AddonGitHubTooltip"] = ("Quellcode und Releases des WoW-Addons bei GitHub öffnen", "Open the WoW addon's source code and releases on GitHub"),
        ["CompanionGitHubTooltip"] = ("Hourstone Companion bei GitHub öffnen", "Open Hourstone Companion on GitHub"),
        ["ExternalBrowserHint"] = ("Öffnet eine Webseite in deinem Standardbrowser.", "Opens a website in your default browser."),
        ["InstallAddonOnCurseForge"] = ("Addon bei CurseForge installieren", "Install addon via CurseForge"),
        ["UpdateAddonOnCurseForge"] = ("Addon bei CurseForge aktualisieren", "Update addon via CurseForge"),
        ["AccountSources"] = ("Gespeicherte Account-Quellen", "Saved account sources"),
        ["BrowserOpenFailed"] = ("Der Standardbrowser konnte nicht geöffnet werden. Öffne diesen Link in deinem Browser: {0}", "Your default browser could not be opened. Open this link in your browser: {0}"),
        ["FirstSave"] = ("Hourstone 0.2.2 oder neuer muss einmal im jeweiligen Client geladen und durch Ausloggen oder /reload gespeichert werden. Originaldateien bleiben unter der Kontrolle von WoW.", "Load Hourstone 0.2.2 or later in each client, then log out or /reload once. WoW remains in control of its original files."),
        ["SyncIntro"] = ("Hourstone speichert Austauschdateien in einem Ordner auf deinem PC. Dropbox, OneDrive, Proton Drive oder ein anderer Synchronisierungsdienst überträgt diesen Ordner auf deine weiteren Geräte. Dort übernimmt der Companion die Daten automatisch.", "Hourstone saves exchange files in a folder on your PC. Dropbox, OneDrive, Proton Drive or another sync service transfers this folder to your other devices. The Companion on each device then picks up the data automatically."),
        ["Folder"] = ("Gemeinsamer Syncordner", "Shared sync folder"),
        ["ChooseFolder"] = ("Ordner auswählen", "Choose folder"),
        ["Pause"] = ("Pausieren", "Pause"),
        ["Resume"] = ("Fortsetzen", "Resume"),
        ["Detach"] = ("Verbindung trennen", "Disconnect"),
        ["OfflineFolder"] = ("Dropbox: „Offline verfügbar machen“. OneDrive und Proton Drive: „Immer auf diesem Gerät behalten“. Wähle diese Einstellung für den gesamten Sync-Ordner auf beiden PCs. Eine Datei in diesem Ordner bestätigt noch nicht, dass ein anderer PC sie empfangen hat.", "Dropbox: Make available offline. OneDrive and Proton Drive: Always keep on this device. Apply this setting to the entire sync folder on both PCs. A file saved in this folder does not confirm that another PC has received it."),
        ["Devices"] = ("Bekannte Geräte", "Known devices"),
        ["DeviceName"] = ("Gerätename", "Device name"),
        ["Appearance"] = ("Darstellung", "Appearance"),
        ["Language"] = ("Sprache", "Language"),
        ["Autostart"] = ("Mit Windows im Hintergrund starten", "Start in the background with Windows"),
        ["Save"] = ("Änderungen speichern", "Save changes"),
        ["SaveChanges"] = ("Änderungen speichern", "Save changes"),
        ["SettingsSaved"] = ("Änderungen gespeichert", "Changes saved"),
        ["SettingsSaveFailed"] = ("Einstellungen konnten nicht vollständig gespeichert werden. Deine Eingaben bleiben erhalten. Bitte versuche es erneut.", "Settings could not be fully saved. Your entries are kept. Please try again."),
        ["SettingsInvalidName"] = ("Der Gerätename ist leer, zu lang oder enthält ungültige Zeichen.", "The device name is empty, too long, or contains invalid characters."),
        ["SettingsUnsaved"] = ("Ungespeicherte Änderungen", "Unsaved changes"),
        ["SettingsSaveHint"] = ("Änderungen werden erst mit „Änderungen speichern“ übernommen.", "Selections take effect when you choose Save changes."),
        ["CaptionMinimize"] = ("Minimieren", "Minimize"),
        ["CaptionMaximize"] = ("Maximieren", "Maximize"),
        ["CaptionRestore"] = ("Wiederherstellen", "Restore"),
        ["CaptionClose"] = ("Schließen", "Close"),
        ["CaptionCloseTooltip"] = ("Schließen (Alt+F4) · im Infobereich weiterlaufen", "Close (Alt+F4) · keep running in the notification area"),
        ["Updates"] = ("Automatische Updates", "Automatic updates"),
        ["UpdateInfo"] = ("Hourstone Companion sucht beim Start und danach täglich auf GitHub nach einer neuen veröffentlichten App-Version und lädt sie automatisch herunter. Nach einem Hinweis wird das Update installiert, sobald WoW geschlossen ist und du das App-Fenster nicht mehr verwendest. Ungespeicherte Einstellungen verhindern den Neustart. Anschließend startet die App im Hintergrund neu.", "Hourstone Companion checks GitHub at startup and daily for a newly published app version and downloads it automatically. After a notice, the update installs once WoW is closed and you are no longer using the app window. Unsaved settings prevent the restart. The app then restarts in the background."),
        ["CheckUpdate"] = ("Nach App-Updates suchen", "Check for app updates"),
        ["Diagnostics"] = ("Lokale Diagnose", "Local diagnostics"),
        ["Credits"] = ("Credits", "Credits"),
        ["BlizzardCredit"] = ("World of Warcraft ist eine Marke oder eingetragene Marke von Blizzard Entertainment, Inc.\nSpiel-Icons: © Blizzard Entertainment, Inc.\nHourstone Companion ist ein unabhängiges Projekt ohne Verbindung zu oder Unterstützung durch Blizzard.", "World of Warcraft is a trademark or registered trademark of Blizzard Entertainment, Inc.\nGame icons: © Blizzard Entertainment, Inc.\nHourstone Companion is an independent project, unaffiliated with and not endorsed by Blizzard."),
        ["Footer"] = ("Neue Spielzeit wird nach dem Ausloggen oder einem /reload übernommen.", "New playtime is picked up after logout or /reload."),
        ["AllClients"] = ("Alle Clients", "All clients"),
        ["AllRealms"] = ("Alle Realms", "All realms"),
        ["Active"] = ("●  Automatischer Abgleich aktiv", "●  Automatic sync active"),
        ["Ready"] = ("Daten bereitgestellt", "Data ready"),
        ["Unconfigured"] = ("Clients einrichten", "Set up clients"),
        ["Busy"] = ("Daten werden geprüft …", "Checking data …"),
        ["Attention"] = ("Abgleich benötigt Aufmerksamkeit", "Sync needs attention"),
        ["NoneFolder"] = ("Kein Syncordner verbunden. Der lokale Abgleich bleibt aktiv.", "No sync folder connected. Local sync remains active."),
        ["NoSources"] = ("Noch keine gespeicherten Hourstone-Daten gefunden.", "No saved Hourstone data found yet."),
        ["NoDevices"] = ("Weitere Geräte erscheinen, sobald ihre Austauschdatei hier empfangen wurde.", "Other devices appear after their exchange file has been received here."),
        ["CharacterHeader"] = ("Charakter", "Character"),
        ["TimeHeader"] = ("Spielzeit", "Playtime"),
        ["UpdatedHeader"] = ("Aktualisiert", "Updated"),
        ["Now"] = ("gerade eben", "just now"),
        ["LastCheck"] = ("Letzter Abgleich", "Last sync check"),
        ["CheckTooltip"] = ("Liest gespeicherte Hourstone-Daten aller ausgewählten Accounts ein, gleicht verfügbare Daten anderer PCs ab und aktualisiert die gemeinsame Übersicht sowie die Daten für das WoW-Addon.", "Reads saved Hourstone data from every selected account, exchanges available data from other PCs, and updates the shared overview and the data for the WoW addon."),
        ["CheckSaveHint"] = ("WoW muss neue Spielzeit zuerst durch Ausloggen oder /reload speichern. „Daten abgleichen“ erzwingt weder diesen Speichervorgang noch die Übertragung durch Dropbox, OneDrive oder Proton Drive.", "WoW must save new playtime first through logout or /reload. Sync data does not force WoW to save or Dropbox, OneDrive or Proton Drive to transfer files."),
        ["SyncStepFirst"] = ("Erster PC: Wähle einen Ordner, der bereits durch deinen Dienst synchronisiert wird. Der Companion erstellt darin den Unterordner HourstoneSync.", "First PC: Choose a folder that your service already synchronizes. The Companion creates the HourstoneSync subfolder inside it."),
        ["SyncStepOther"] = ("Weitere PCs: Warte, bis HourstoneSync vollständig angekommen ist. Wähle dann denselben synchronisierten Ordner oder direkt den Unterordner HourstoneSync aus.", "Other PCs: Wait until HourstoneSync has fully arrived. Then choose the same synchronized folder or the HourstoneSync subfolder itself."),
        ["SyncStepAvailability"] = ("Alle Geräte: Halte den Ordner dauerhaft lokal verfügbar. Companion und Synchronisierungsdienst müssen für den Austausch laufen.", "All devices: Keep the folder permanently available locally. The Companion and the sync service must be running to exchange data."),
        ["SyncAccountHint"] = ("Du meldest dich bei deinem Synchronisierungsdienst an. Ein eigenes Companion-Konto wird nicht benötigt.", "Sign in through your sync service. No separate Companion account is required."),
        ["SyncLocalHint"] = ("Auch ohne gemeinsamen Ordner gleicht der Companion die ausgewählten WoW-Accounts auf diesem PC ab.", "Without a shared folder, the Companion still syncs the selected WoW accounts on this PC."),
        ["SyncPauseHint"] = ("Pausieren unterbricht nur den Austausch mit anderen PCs. Bereits empfangene Daten bleiben erhalten. Beim Trennen werden fremde Beiträge lokal ausgeblendet; Dateien anderer PCs bleiben unangetastet.", "Pausing stops only the exchange with other PCs and keeps received data. Disconnecting hides other PCs’ contributions locally without changing their files."),
        ["SyncFolderLabel"] = ("Verwendeter Ordner", "Folder in use"),
        ["SyncWoWTitle"] = ("Anzeige im WoW-Addon", "Display in the WoW addon"),
        ["SyncWoWLoadHint"] = ("WoW übernimmt diesen Stand beim nächsten Login oder /reload. Nach der ersten Einrichtung WoW einmal vollständig schließen und neu starten.", "WoW loads this data on your next login or /reload. After the first setup, fully close and restart WoW once."),
        ["WoWOverviewSaved"] = ("Gemeinsame Übersicht für das WoW-Addon gespeichert", "Shared overview saved for the WoW addon"),
        ["WoWOverviewPending"] = ("Die gemeinsame Übersicht konnte noch nicht für alle ausgewählten Clients gespeichert werden. Unter „Clients“ findest du die nächsten Schritte; Fehlerdetails stehen in der lokalen Diagnose.", "The shared overview could not yet be saved for every selected client. Open Clients for the next steps; error details are in local diagnostics."),
        ["WoWOverviewUnconfigured"] = ("Wähle unter „Clients“ die WoW-Accounts aus, deren Daten du gemeinsam anzeigen möchtest.", "Open Clients and select the WoW accounts whose data you want to view together."),
        ["WoWOverviewUnchecked"] = ("Die gemeinsame Übersicht wird beim nächsten Datenabgleich für das WoW-Addon gespeichert.", "The shared overview will be saved for the WoW addon during the next sync check."),
        ["CloudPaused"] = ("Ordneraustausch pausiert. Empfangene Daten bleiben erhalten.", "Folder exchange paused. Received data is kept."),
        ["CloudDisconnected"] = ("Der Austausch mit anderen PCs ist noch nicht eingerichtet.", "Exchange with other PCs has not been set up yet."),
        ["CloudPublished"] = ("Daten im Syncordner verfügbar", "Data available in sync folder"),
        ["CheckedAt"] = ("Geprüft um {0}", "Checked at {0}"),
        ["CloudPending"] = ("Speichern im Syncordner ausstehend. Der letzte gültige Stand bleibt erhalten.", "Saving to the sync folder is pending. The last valid data is kept."),
        ["CloudFailed"] = ("Der Syncordner ist momentan nicht erreichbar. Der lokale Abgleich läuft weiter; zuletzt empfangene Daten bleiben erhalten.", "The sync folder is currently unavailable. Local sync continues and previously received data is kept."),
        ["AddonUpdateHint"] = ("Das WoW-Addon aktualisierst du separat über CurseForge.", "Update the WoW addon separately through CurseForge."),
        ["ThisPC"] = ("Dieser PC", "This PC"),
    };
    public Dictionary<string, string> T => Strings.ToDictionary(x => x.Key, x => English ? x.Value.En : x.Value.De);
    public string Text(string key) => English ? Strings[key].En : Strings[key].De;
    public string DeviceName { get; set; } = Environment.MachineName;
    public ProgressViewModel Progress { get; } = new();
    bool showProgress;
    public bool ShowProgress
    {
        get => showProgress;
        set { if (showProgress == value) return; showProgress = value; Changed(); Changed(nameof(ShowPlaytime)); Changed(nameof(PlaytimeVisibility)); Changed(nameof(ProgressVisibility)); Changed(nameof(OverviewSubtitle)); }
    }
    public bool ShowPlaytime => !ShowProgress;
    public Visibility PlaytimeVisibility => ShowProgress ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ProgressVisibility => ShowProgress ? Visibility.Visible : Visibility.Collapsed;
    public string OverviewSubtitle => ShowProgress ? Progress.Subtitle : Text("Subtitle");
    public string PlaytimeTab => English ? "Playtime" : "Spielzeit";
    public string ProgressTab => English ? "Progress" : "Fortschritt";
    public bool IsLight { get; private set; }
    public void SetLight(bool light) { IsLight = light; RefreshRows(); Progress.SetAppearance(English, light); }
    public bool Demo { get; }
    public string BuildLabel => "v" + typeof(MainViewModel).Assembly.GetName().Version!.ToString(3);
    public string PreviewLabel => Demo ? (English ? "Preview · sample data" : "Vorschau · Beispieldaten") : "";
    List<Observation> observations = [], removedObservations = [];
    IEnumerable<Observation> DisplayObservations => showRemoved ? removedObservations : observations;
    bool showRemoved;
    CharacterRow? selectedRow;
    public CharacterRow? SelectedRow { get => selectedRow; set { selectedRow = value; Changed(); Changed(nameof(CanChangeCharacter)); } }
    public bool CanChangeCharacter => IsIdle && SelectedRow != null;
    public bool ShowRemoved
    {
        get => showRemoved;
        set { if (showRemoved == value) return; showRemoved = value; RebuildFilters(); Changed(); Changed(nameof(ListTitle)); Changed(nameof(ToggleRemovedLabel)); Changed(nameof(CharacterActionLabel)); }
    }
    public string ListTitle => Text(showRemoved ? "RemovedCharacters" : "Characters");
    public string ToggleRemovedLabel => showRemoved ? Text("BackToCharacters") : Text("RemovedCharacters") + " (" + removedObservations.Count + ")";
    public string CharacterActionLabel => Text(showRemoved ? "RestoreCharacter" : "RemoveCharacter");
    public string EmptyTitle => Text(showRemoved ? "RemovedCharacters" : "EmptyTitle");
    public IReadOnlyList<Observation> VisibleObservations => observations.ToArray();
    public IReadOnlyList<Observation> RemovedObservations => removedObservations.ToArray();
    public ObservableCollection<CharacterRow> Rows { get; } = [];
    public ObservableCollection<string> Clients { get; } = [];
    public ObservableCollection<string> Realms { get; } = [];
    string search = "", selectedClient = "", selectedRealm = "";
    bool hours = true, idle = true;
    public bool IsIdle { get => idle; set { idle = value; Changed(); Changed(nameof(CanChangeCharacter)); } }
    public string Search { get => search; set { search = value; RefreshRows(); Changed(); Changed(nameof(SearchHintVisibility)); } }
    public string SelectedClient { get => selectedClient; set { selectedClient = value; RefreshRows(); } }
    public string SelectedRealm { get => selectedRealm; set { selectedRealm = value; RefreshRows(); } }
    public Visibility SearchHintVisibility => string.IsNullOrEmpty(Search) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public string EmptyMessage => Text(showRemoved ? (removedObservations.Count == 0 ? "RemovedEmpty" : "NoMatches") : observations.Count == 0 ? "EmptyText" : "NoMatches");
    public string TotalTime => (observations.Sum(x => x.Seconds) / 3600).ToString("N0", Culture) + (English ? " hrs" : " Std.");
    public string TotalDays { get { var h = (long)(observations.Sum(x => x.Seconds) / 3600); return English ? $"{(h / 24).ToString("N0", Culture)} days · {h % 24} hours" : $"{(h / 24).ToString("N0", Culture)} Tage · {h % 24} Stunden"; } }
    public int CharacterCount => observations.Count;
    public int ClientCount => observations.Select(x => x.Flavor).Distinct().Count();
    public string RowSummary => showRemoved ? $"{Rows.Count} {Text("RemovedCharacters")}" : $"{Rows.Count} {Text("Characters")} · {Rows.Select(x => x.Client).Distinct().Count()} Clients";
    string status = "";
    DateTimeOffset? lastCompletedCheck;
    public string Status { get => status; set { status = value; Changed(); } }
    public string LastSync => lastCompletedCheck is { } completed ? Text("LastCheck") + " · " + completed.ToLocalTime().ToString("HH:mm", Culture) : "";
    public string CloudAvailability(DateTimeOffset completedAt) => Text("CloudPublished") + " · " + string.Format(Culture, Text("CheckedAt"), completedAt.ToLocalTime().ToString("HH:mm", Culture));
    public void RecordCheck(DateTimeOffset completedAt) { lastCompletedCheck = completedAt; Changed(nameof(LastSync)); }
    public bool IsHours => hours;
    public bool IsDays => !hours;
    public string HoursBackground => hours ? "#34C9F3" : "Transparent";
    public string HoursForeground => hours ? "#071B28" : EnglishColor;
    public string DaysBackground => !hours ? "#34C9F3" : "Transparent";
    public string DaysForeground => !hours ? "#071B28" : EnglishColor;
    string EnglishColor => "#9BAFC1";
    public MainViewModel(bool demo)
    {
        Demo = demo; SetObservations(demo ? DemoData() : []);
        if (demo) Progress.SetProgress(ProgressViewModel.DemoProgress(VisibleObservations));
        Status = Text(demo ? "Active" : "Unconfigured");
        if (demo) RecordCheck(new DateTimeOffset(DateTime.Today.AddHours(14).AddMinutes(32)));
    }
    public void SetLanguage(bool english)
    {
        var statusKey = Strings.FirstOrDefault(x => (English ? x.Value.En : x.Value.De) == Status).Key;
        English = english; Changed(nameof(T));
        Progress.SetAppearance(english, IsLight); Changed(nameof(OverviewSubtitle)); Changed(nameof(PlaytimeTab)); Changed(nameof(ProgressTab));
        if (statusKey != null) Status = Text(statusKey);
        Changed(nameof(LastSync));
        SetObservations(observations.ToArray(), removedObservations.ToArray()); Changed(nameof(BuildLabel)); Changed(nameof(PreviewLabel));
    }
    public void SetHours(bool value) { hours = value; RefreshRows(); Changed(nameof(IsHours)); Changed(nameof(IsDays)); Changed(nameof(HoursBackground)); Changed(nameof(HoursForeground)); Changed(nameof(DaysBackground)); Changed(nameof(DaysForeground)); }
    public void NotifyDevice() => Changed(nameof(DeviceName));
    public void SetObservations(IEnumerable<Observation> values, IEnumerable<Observation>? removedValues = null)
    {
        observations = values.ToList(); removedObservations = removedValues?.ToList() ?? [];
        Progress.SetCharacters(observations);
        RebuildFilters();
        Changed(nameof(TotalTime)); Changed(nameof(TotalDays)); Changed(nameof(CharacterCount)); Changed(nameof(ClientCount));
        Changed(nameof(ListTitle)); Changed(nameof(ToggleRemovedLabel)); Changed(nameof(CharacterActionLabel));
    }
    void RebuildFilters()
    {
        var oldClient = selectedClient; var oldRealm = selectedRealm;
        Clients.Clear(); Clients.Add(Text("AllClients")); foreach (var c in DisplayObservations.Select(x => ClientName(x.Flavor)).Distinct().Order()) Clients.Add(c);
        Realms.Clear(); Realms.Add(Text("AllRealms")); foreach (var r in DisplayObservations.Select(x => x.Realm).Distinct().Order()) Realms.Add(r);
        selectedClient = Clients.Contains(oldClient) ? oldClient : Clients[0]; selectedRealm = Realms.Contains(oldRealm) ? oldRealm : Realms[0];
        Changed(nameof(SelectedClient)); Changed(nameof(SelectedRealm)); RefreshRows();
        Changed(nameof(TotalTime)); Changed(nameof(TotalDays)); Changed(nameof(CharacterCount)); Changed(nameof(ClientCount));
    }
    void RefreshRows()
    {
        var selectedIdentity = SelectedRow is null ? null : ObservationRules.Identity(SelectedRow.Value);
        Rows.Clear(); foreach (var o in DisplayObservations.Where(x => (x.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) || (x.Guild?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)) && (string.IsNullOrEmpty(selectedClient) || selectedClient == Text("AllClients") || ClientName(x.Flavor) == selectedClient) && (string.IsNullOrEmpty(selectedRealm) || selectedRealm == Text("AllRealms") || x.Realm == selectedRealm)).OrderByDescending(x => x.Seconds).ThenBy(x => x.Name)) Rows.Add(new(o, this, hours));
        SelectedRow = Rows.FirstOrDefault(row => ObservationRules.Identity(row.Value) == selectedIdentity);
        Changed(nameof(EmptyVisibility)); Changed(nameof(EmptyMessage)); Changed(nameof(EmptyTitle)); Changed(nameof(RowSummary));
    }
    public static string ClientName(string flavor) => flavor switch { "retail" => "Retail", "mists" => "Mists Classic", "tbc" => "TBC Anniversary", "era" => "Classic Era", _ => flavor };
    public static List<Observation> AllClassDemoData()
    {
        string[] classes = ["WARRIOR", "PALADIN", "HUNTER", "ROGUE", "PRIEST", "DEATHKNIGHT", "SHAMAN", "MAGE", "WARLOCK", "MONK", "DRUID", "DEMONHUNTER", "EVOKER"];
        string[] flavors = ["retail", "mists", "tbc", "era"];
        return classes.Select((token, i) => new Observation
        {
            SourceId = "sample-source", Region = "eu", Guid = "Player-0000-CLASS" + i,
            Name = ClassNameForPreview(token), Realm = "Azeroth", Class = token,
            Flavor = token is "EVOKER" or "DEMONHUNTER" ? "retail" : token is "MONK" or "DEATHKNIGHT" ? "mists" : flavors[i % flavors.Length],
            Level = 60, Seconds = (13 - i) * 3600, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Guild = i % 4 == 0 ? null : i % 4 == 1 ? "" : "Hüter der Morgenröte",
            GuildUpdatedAt = i % 4 == 0 ? null : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }).ToList();
    }
    private static string ClassNameForPreview(string token) => CharacterRow.ClassName(token, false);

    public static List<Observation> DemoData()
    {
        string[] names = ["Thalindra", "Morgath", "Lunara", "Elarion", "Schattenwind", "Bronzebart", "Funkenflug", "Fluchmatrose"];
        string[] realms = ["Antonidas", "Blackhand", "Everlook", "Lakeshire", "Thunderstrike", "Thunderstrike", "Celebras", "Everlook"];
        string[] classes = ["DEMONHUNTER", "WARRIOR", "DRUID", "MAGE", "ROGUE", "PALADIN", "HUNTER", "WARLOCK"];
        string[] flavors = ["retail", "retail", "mists", "mists", "tbc", "tbc", "era", "era"];
        string?[] guilds = ["Hüter der Morgenröte", "Hüter der Morgenröte", "Sternenwanderer", "", "Bund der Dämmerung", "Bund der Dämmerung", null, "Abenteurerbund"];
        int[] minutes = [50715, 32060, 18765, 12310, 7710, 4445, 2620, 675];
        int[] age = [120, 3600, 86400, 86400, 10800, 10800, 172800, 172800];
        return Enumerable.Range(0, 8).Select(i => new Observation { SourceId = "sample-source", Region = "eu", Guid = "Player-0000-" + i, Name = names[i], Realm = realms[i], Class = classes[i], Flavor = flavors[i], Level = i < 2 ? 90 : i < 4 ? 90 : i < 6 ? 70 : 60, Seconds = minutes[i] * 60, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - age[i], Guild = guilds[i], GuildUpdatedAt = guilds[i] is null ? null : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - age[i] }).ToList();
    }
}

public sealed class CharacterRow
{
    public Observation Value { get; }
    public string Name => Value.Name;
    readonly bool light, english;
    public string Details => $"{Value.Realm} · {Value.Level} {ClassName(Value.Class, english)}";
    public string Guild => Value.Guild switch
    {
        null => english ? "Not yet recorded" : "Noch nicht erfasst",
        "" => english ? "No guild" : "Keine Gilde",
        var name => name
    };
    public string GuildLine => (english ? "Guild: " : "Gilde: ") + Guild;
    public string Tooltip => $"{Name}\n{Details}\n{GuildLine}" + (Value.Guild is null
        ? (english ? "\nLog in with this character using Hourstone 0.2.1 or later, then log out or /reload." : "\nDiesen Charakter mit Hourstone 0.2.1 oder neuer einloggen, dann ausloggen oder /reload ausführen.") : "");
    public static string ClassName(string token, bool english) => token switch
    {
        "DEMONHUNTER" => english ? "Demon Hunter" : "Dämonenjäger",
        "WARRIOR" => english ? "Warrior" : "Krieger",
        "DRUID" => english ? "Druid" : "Druide",
        "MAGE" => english ? "Mage" : "Magier",
        "ROGUE" => english ? "Rogue" : "Schurke",
        "PALADIN" => "Paladin",
        "HUNTER" => english ? "Hunter" : "Jäger",
        "WARLOCK" => english ? "Warlock" : "Hexenmeister",
        "PRIEST" => english ? "Priest" : "Priester",
        "SHAMAN" => english ? "Shaman" : "Schamane",
        "DEATHKNIGHT" => english ? "Death Knight" : "Todesritter",
        "MONK" => english ? "Monk" : "Mönch",
        "EVOKER" => english ? "Evoker" : "Rufer",
        _ => token
    };
    public double Seconds => Value.Seconds;
    public double UpdatedAt => Value.UpdatedAt;
    public string Client => MainViewModel.ClientName(Value.Flavor);
    public string ClassIconPath => IconCatalog.ClassIconPath(Value.Class);
    public string ClientIconPath => IconCatalog.ClientIconPath(Value.Flavor);
    public string Color => light ? Value.Class switch { "DEMONHUNTER" => "#793F9C", "WARRIOR" => "#AD3930", "DRUID" => "#48691C", "MAGE" => "#167395", "ROGUE" => "#687018", "PALADIN" => "#945609", "HUNTER" => "#97541B", "WARLOCK" => "#3A61A6", _ => "#456878" } : Value.Class switch { "DEMONHUNTER" => "#CC91FF", "WARRIOR" => "#F17D72", "DRUID" => "#AAD74B", "MAGE" => "#63D4F3", "ROGUE" => "#D6E67B", "PALADIN" => "#FFBD68", "HUNTER" => "#FBB25B", "WARLOCK" => "#8CB9FF", "PRIEST" => "#E6E6EA", "SHAMAN" => "#71AEF2", "DEATHKNIGHT" => "#EE7182", "MONK" => "#60DAB3", "EVOKER" => "#70C1B0", _ => "#B8CEDC" };
    public string Time { get; }
    public string Age { get; }
    public CharacterRow(Observation o, MainViewModel m, bool hours)
    {
        Value = o; light = m.IsLight; english = m.English; var minutes = (long)(o.Seconds / 60); var h = minutes / 60;
        Time = hours ? (m.English ? $"{h.ToString("N0", m.Culture)} hrs {minutes % 60:00} min" : $"{h.ToString("N0", m.Culture)} Std. {minutes % 60:00} Min.") : (m.English ? $"{h / 24}d {h % 24}h" : $"{h / 24} T. {h % 24} Std.");
        var age = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - o.UpdatedAt);
        Age = age < 60 ? m.Text("Now") : age < 3600 ? (m.English ? $"{(int)(age / 60)} min ago" : $"vor {(int)(age / 60)} Min.") : age < 86400 ? (m.English ? $"{(int)(age / 3600)} hours ago" : $"vor {(int)(age / 3600)} Std.") : age < 172800 ? (m.English ? "yesterday" : "gestern") : (m.English ? $"{(int)(age / 86400)} days ago" : $"vor {(int)(age / 86400)} Tagen");
    }
}
