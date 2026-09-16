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
        ["Sync"] = ("Synchronisierung", "Synchronization"),
        ["Settings"] = ("Einstellungen", "Settings"),
        ["Local"] = ("Lokal gespeichert", "Stored locally"),
        ["Hero"] = ("Deine Zeit in Azeroth", "Your time in Azeroth"),
        ["Subtitle"] = ("Alle Charaktere. Alle Clients. Ein Überblick.", "Every character. Every client. One overview."),
        ["CheckNow"] = ("Jetzt prüfen", "Check now"),
        ["SavedTime"] = ("Gespeicherte Spielzeit", "Saved playtime"),
        ["Characters"] = ("Charaktere", "Characters"),
        ["Search"] = ("Charakter suchen …", "Search characters …"),
        ["Hours"] = ("Stunden", "Hours"),
        ["Days"] = ("Tage + Std.", "Days + hours"),
        ["EmptyTitle"] = ("Dein Überblick beginnt hier", "Your overview starts here"),
        ["EmptyText"] = ("Wähle unter Clients deinen WoW-Ordner aus. Nach dem Ausloggen oder /reload erscheint deine gespeicherte Spielzeit.", "Select your WoW folder under Clients. Your saved playtime appears after logout or /reload."),
        ["NoMatches"] = ("Keine Charaktere für diese Filter gefunden.", "No characters match these filters."),
        ["ClientsIntro"] = ("Wähle die lokalen WoW-Installationen und Accounts, deren Spielzeit du zusammen anzeigen möchtest.", "Select the local WoW installations and accounts you want to include."),
        ["AddWoW"] = ("WoW-Ordner hinzufügen", "Add WoW folder"),
        ["Discover"] = ("Installationen suchen", "Find installations"),
        ["FirstSave"] = ("Hourstone 0.2 oder neuer muss einmal im jeweiligen Client geladen und durch Ausloggen oder /reload gespeichert werden. Originaldateien bleiben unter der Kontrolle von WoW.", "Load Hourstone 0.2 or later in each client, then log out or /reload once. WoW remains in control of its original files."),
        ["SyncIntro"] = ("Verbinde deine PCs über einen gemeinsamen Dropbox- oder OneDrive-Ordner. Der Companion benötigt dafür kein eigenes Konto.", "Connect your PCs through a shared Dropbox or OneDrive folder. No Companion account is required."),
        ["Folder"] = ("Gemeinsamer Syncordner", "Shared sync folder"),
        ["ChooseFolder"] = ("Ordner auswählen", "Choose folder"),
        ["Pause"] = ("Pausieren", "Pause"),
        ["Resume"] = ("Fortsetzen", "Resume"),
        ["Detach"] = ("Verbindung trennen", "Disconnect"),
        ["OfflineFolder"] = ("Wichtig für den Austausch: Dropbox → „Offline verfügbar machen“ oder OneDrive → „Immer auf diesem Gerät behalten“. Bereitgestellte Dateien sind noch keine Empfangsbestätigung des anderen PCs.", "For file exchange: Dropbox → Make available offline, or OneDrive → Always keep on this device. Publishing a file does not confirm receipt by another PC."),
        ["Devices"] = ("Bekannte Geräte", "Known devices"),
        ["DeviceName"] = ("Gerätename", "Device name"),
        ["Appearance"] = ("Darstellung", "Appearance"),
        ["Language"] = ("Sprache", "Language"),
        ["Autostart"] = ("Mit Windows im Hintergrund starten", "Start in the background with Windows"),
        ["Save"] = ("Einstellungen speichern", "Save settings"),
        ["Updates"] = ("Automatische Updates", "Automatic updates"),
        ["UpdateInfo"] = ("Prüfung beim Start und täglich. Neue Versionen werden nach Hinweis installiert, sobald WoW geschlossen und dieses Fenster nicht in Benutzung ist.", "Checked at startup and daily. Updates install after a notice, once WoW is closed and this window is idle."),
        ["CheckUpdate"] = ("Nach Updates suchen", "Check for updates"),
        ["Diagnostics"] = ("Lokale Diagnose", "Local diagnostics"),
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
        ["NoDevices"] = ("Weitere Geräte erscheinen nach ihrem ersten gespeicherten Snapshot.", "Other devices appear after their first saved snapshot."),
        ["CharacterHeader"] = ("Charakter", "Character"),
        ["TimeHeader"] = ("Spielzeit", "Playtime"),
        ["UpdatedHeader"] = ("Aktualisiert", "Updated"),
        ["Now"] = ("gerade eben", "just now"),
        ["ThisPC"] = ("Dieser PC", "This PC"),
    };
    public Dictionary<string, string> T => Strings.ToDictionary(x => x.Key, x => English ? x.Value.En : x.Value.De);
    public string Text(string key) => English ? Strings[key].En : Strings[key].De;
    public string DeviceName { get; set; } = Environment.MachineName;
    public bool IsLight { get; private set; }
    public void SetLight(bool light) { IsLight = light; RefreshRows(); }
    public bool Demo { get; }
    public string BuildLabel => Demo ? (English ? "Preview · sample data" : "Vorschau · Beispieldaten") : "v0.1.0";
    List<Observation> observations = [];
    public ObservableCollection<CharacterRow> Rows { get; } = [];
    public ObservableCollection<string> Clients { get; } = [];
    public ObservableCollection<string> Realms { get; } = [];
    string search = "", selectedClient = "", selectedRealm = "";
    bool hours = true, idle = true;
    public bool IsIdle { get => idle; set { idle = value; Changed(); } }
    public string Search { get => search; set { search = value; RefreshRows(); Changed(); Changed(nameof(SearchHintVisibility)); } }
    public string SelectedClient { get => selectedClient; set { selectedClient = value; RefreshRows(); } }
    public string SelectedRealm { get => selectedRealm; set { selectedRealm = value; RefreshRows(); } }
    public Visibility SearchHintVisibility => string.IsNullOrEmpty(Search) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public string EmptyMessage => Text(observations.Count == 0 ? "EmptyText" : "NoMatches");
    public string TotalTime => (observations.Sum(x => x.Seconds) / 3600).ToString("N0", Culture) + (English ? " hrs" : " Std.");
    public string TotalDays { get { var h = (long)(observations.Sum(x => x.Seconds) / 3600); return English ? $"{(h / 24).ToString("N0", Culture)} days · {h % 24} hours" : $"{(h / 24).ToString("N0", Culture)} Tage · {h % 24} Stunden"; } }
    public int CharacterCount => observations.Count;
    public int ClientCount => observations.Select(x => x.Flavor).Distinct().Count();
    public string RowSummary => $"{Rows.Count} {Text("Characters")} · {Rows.Select(x => x.Client).Distinct().Count()} Clients";
    string status = "", lastSync = "";
    public string Status { get => status; set { status = value; Changed(); } }
    public string LastSync { get => lastSync; set { lastSync = value; Changed(); } }
    public string HoursBackground => hours ? "#34C9F3" : "Transparent";
    public string HoursForeground => hours ? "#071B28" : EnglishColor;
    public string DaysBackground => !hours ? "#34C9F3" : "Transparent";
    public string DaysForeground => !hours ? "#071B28" : EnglishColor;
    string EnglishColor => "#9BAFC1";
    public MainViewModel(bool demo)
    {
        Demo = demo; SetObservations(demo ? DemoData() : []);
        Status = Text(demo ? "Active" : "Unconfigured"); LastSync = demo ? Text("Ready") + " · 14:32" : "";
    }
    public void SetLanguage(bool english)
    {
        var statusKey = Strings.FirstOrDefault(x => (English ? x.Value.En : x.Value.De) == Status).Key;
        var ready = Text("Ready");
        English = english; Changed(nameof(T));
        if (statusKey != null) Status = Text(statusKey);
        LastSync = LastSync.Replace(ready, Text("Ready"), StringComparison.Ordinal);
        SetObservations(observations.ToArray()); Changed(nameof(BuildLabel));
    }
    public void SetHours(bool value) { hours = value; RefreshRows(); Changed(nameof(HoursBackground)); Changed(nameof(HoursForeground)); Changed(nameof(DaysBackground)); Changed(nameof(DaysForeground)); }
    public void NotifyDevice() => Changed(nameof(DeviceName));
    public void SetObservations(IEnumerable<Observation> values)
    {
        observations = values.ToList();
        var oldClient = selectedClient; var oldRealm = selectedRealm;
        Clients.Clear(); Clients.Add(Text("AllClients")); foreach (var c in observations.Select(x => ClientName(x.Flavor)).Distinct().Order()) Clients.Add(c);
        Realms.Clear(); Realms.Add(Text("AllRealms")); foreach (var r in observations.Select(x => x.Realm).Distinct().Order()) Realms.Add(r);
        selectedClient = Clients.Contains(oldClient) ? oldClient : Clients[0]; selectedRealm = Realms.Contains(oldRealm) ? oldRealm : Realms[0];
        Changed(nameof(SelectedClient)); Changed(nameof(SelectedRealm)); RefreshRows();
        Changed(nameof(TotalTime)); Changed(nameof(TotalDays)); Changed(nameof(CharacterCount)); Changed(nameof(ClientCount));
    }
    void RefreshRows()
    {
        Rows.Clear(); foreach (var o in observations.Where(x => x.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) && (string.IsNullOrEmpty(selectedClient) || selectedClient == Text("AllClients") || ClientName(x.Flavor) == selectedClient) && (string.IsNullOrEmpty(selectedRealm) || selectedRealm == Text("AllRealms") || x.Realm == selectedRealm)).OrderByDescending(x => x.Seconds).ThenBy(x => x.Name)) Rows.Add(new(o, this, hours));
        Changed(nameof(EmptyVisibility)); Changed(nameof(EmptyMessage)); Changed(nameof(RowSummary));
    }
    public static string ClientName(string flavor) => flavor switch { "retail" => "Retail", "mists" => "Mists Classic", "tbc" => "TBC Anniversary", "era" => "Classic Era", _ => flavor };
    public static List<Observation> DemoData()
    {
        string[] names = ["Thalindra", "Morgath", "Lunara", "Elarion", "Schattenwind", "Bronzebart", "Funkenflug", "Fluchmatrose"];
        string[] realms = ["Antonidas", "Blackhand", "Everlook", "Lakeshire", "Thunderstrike", "Thunderstrike", "Celebras", "Everlook"];
        string[] classes = ["DEMONHUNTER", "WARRIOR", "DRUID", "MAGE", "ROGUE", "PALADIN", "HUNTER", "WARLOCK"];
        string[] flavors = ["retail", "retail", "mists", "mists", "tbc", "tbc", "era", "era"];
        int[] minutes = [50715, 32060, 18765, 12310, 7710, 4445, 2620, 675];
        int[] age = [120, 3600, 86400, 86400, 10800, 10800, 172800, 172800];
        return Enumerable.Range(0, 8).Select(i => new Observation { SourceId = "sample-source", Region = "eu", Guid = "Player-0000-" + i, Name = names[i], Realm = realms[i], Class = classes[i], Flavor = flavors[i], Level = i < 2 ? 90 : i < 4 ? 90 : i < 6 ? 70 : 60, Seconds = minutes[i] * 60, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - age[i] }).ToList();
    }
}

public sealed class CharacterRow
{
    public Observation Value { get; }
    public string Name => Value.Name;
    readonly bool light, english;
    public string Details => $"{Value.Realm} · {Value.Level} {ClassName(Value.Class, english)}";
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
    public string ClientLetter => Value.Flavor switch { "retail" => "R", "mists" => "M", "tbc" => "T", _ => "E" };
    public string ClientColor => light ? Value.Flavor switch { "retail" => "#90630A", "mists" => "#546C76", "tbc" => "#527617", _ => "#AA3C3C" } : Value.Flavor switch { "retail" => "#F5CD66", "mists" => "#BFCBCE", "tbc" => "#A0CC4C", _ => "#EA6363" };
    public string Color => light ? Value.Class switch { "DEMONHUNTER" => "#793F9C", "WARRIOR" => "#AD3930", "DRUID" => "#48691C", "MAGE" => "#167395", "ROGUE" => "#687018", "PALADIN" => "#945609", "HUNTER" => "#97541B", "WARLOCK" => "#3A61A6", _ => "#456878" } : Value.Class switch { "DEMONHUNTER" => "#CC91FF", "WARRIOR" => "#F17D72", "DRUID" => "#AAD74B", "MAGE" => "#63D4F3", "ROGUE" => "#D6E67B", "PALADIN" => "#FFBD68", "HUNTER" => "#FBB25B", "WARLOCK" => "#8CB9FF", "PRIEST" => "#E6E6EA", "SHAMAN" => "#71AEF2", "DEATHKNIGHT" => "#EE7182", "MONK" => "#60DAB3", "EVOKER" => "#70C1B0", _ => "#B8CEDC" };
    public string Glyph => Value.Class switch { "MAGE" => "\uE945", "DRUID" => "\uE8BE", "WARRIOR" => "\uE735", "PALADIN" => "\uE8D7", "HUNTER" => "\uE8B0", _ => "\uE734" };
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
