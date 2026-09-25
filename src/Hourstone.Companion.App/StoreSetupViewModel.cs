using System.ComponentModel;
using System.Windows;

namespace Hourstone.Companion.App;

public sealed class StoreSetupViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool English { get; private set; }
    public string Stage { get; private set; } = "found";
    public int SourceCount { get; set; }
    public bool Fresh { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public string FailureDetail { get; set; } = "";
    public bool IsIdle => Stage != "running";
    public bool RequiresLegacyHandover { get; set; }
    bool legacyHandoverConfirmed;
    public bool LegacyHandoverConfirmed
    {
        get => legacyHandoverConfirmed;
        set { legacyHandoverConfirmed = value; PropertyChanged?.Invoke(this, new(null)); }
    }
    public bool CanContinue => IsIdle && (Stage is not ("prepare" or "fresh") || !RequiresLegacyHandover || LegacyHandoverConfirmed);
    public Visibility HandoverVisibility => RequiresLegacyHandover && Stage is "prepare" or "fresh" or "blocked" ? Visibility.Visible : Visibility.Collapsed;
    public string HandoverInstructions => English
        ? "Disable the previous desktop edition in Windows startup settings. If it is still running, choose Quit from its icon next to the Windows clock. Use the Microsoft Store edition from now on."
        : "Deaktiviere die bisherige Desktop-Ausgabe in den Windows-Autostarteinstellungen. Falls sie noch läuft, wähle an ihrem Symbol neben der Windows-Uhr „Beenden“. Verwende künftig die Ausgabe aus dem Microsoft Store.";
    public string HandoverConfirmation => English
        ? "The previous edition's automatic startup is disabled, or that edition has been uninstalled."
        : "Der Autostart der bisherigen Ausgabe ist deaktiviert oder die Ausgabe wurde deinstalliert.";
    public string StartupSettingsLabel => English ? "Open Windows startup settings" : "Windows-Autostarteinstellungen öffnen";
    public string Welcome => English ? "Welcome to Hourstone" : "Willkommen bei Hourstone";
    public string StoreEdition => English ? "Microsoft Store edition" : "Ausgabe aus dem Microsoft Store";
    public string Steps => English ? "1  Setup     →     2  Transfer     →     3  Ready" : "1  Einrichtung     →     2  Übernahme     →     3  Bereit";
    public string AutostartLabel => English ? "Start in the background with Windows" : "Mit Windows im Hintergrund starten";
    public Visibility AutostartVisibility => Stage is "prepare" or "fresh" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SummaryVisibility => Stage == "found" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => Stage == "error" && FailureDetail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SecondaryVisibility => Stage is "found" or "prepare" or "fresh" or "blocked" or "error" ? Visibility.Visible : Visibility.Collapsed;
    public string Summary => English ? $"{SourceCount} WoW accounts\nShared folder and saved character data\nSettings and hidden characters" : $"{SourceCount} WoW-Accounts\nGemeinsamer Ordner und gespeicherte Charakterdaten\nEinstellungen und ausgeblendete Charaktere";
    public string Title => Stage switch
    {
        "found" => English ? "Transfer your existing setup?" : "Deine bisherige Einrichtung übernehmen?",
        "blocked" => English ? "Close the previous edition first" : "Beende zuerst die bisherige Ausgabe",
        "prepare" => English ? "Use this edition from now on" : "Künftig diese Ausgabe verwenden",
        "fresh" => English ? "Set up Hourstone" : "Hourstone neu einrichten",
        "running" => English ? "Transferring your setup …" : "Deine Einrichtung wird übernommen …",
        "error" => English ? "The transfer could not be completed" : "Die Übernahme hat nicht geklappt",
        _ => English ? "Your setup is ready" : "Deine Einrichtung ist bereit"
    };
    public string Explanation => Stage switch
    {
        "found" => English ? "Hourstone found an existing setup on this PC." : "Hourstone hat eine vorhandene Einrichtung auf diesem PC gefunden.",
        "blocked" => English ? "Open the Hourstone icon next to the Windows clock and choose Quit. Then continue here." : "Öffne das Hourstone-Symbol neben der Windows-Uhr und wähle „Beenden“. Danach kannst du hier fortfahren.",
        "prepare" => English ? "After the transfer, use the Microsoft Store edition. Your existing data is kept." : "Nach der Übernahme verwendest du die Ausgabe aus dem Microsoft Store. Deine bisherigen Daten bleiben erhalten.",
        "fresh" => English ? "Select your WoW accounts and shared folder afterwards. Existing data from the previous edition is kept." : "Wähle anschließend deine WoW-Accounts und den gemeinsamen Ordner aus. Die Daten der bisherigen Ausgabe bleiben erhalten.",
        "running" => English ? "Please leave this window open." : "Bitte lasse dieses Fenster geöffnet.",
        "error" => English ? "Your previous data is still available. See the details below before trying again." : "Deine bisherigen Daten sind weiterhin vorhanden. Beachte die Hinweise unten, bevor du es erneut versuchst.",
        _ => Fresh ? English ? "You can now select your WoW accounts and shared folder." : "Du kannst jetzt deine WoW-Accounts und den gemeinsamen Ordner auswählen." : English ? "Your WoW accounts and settings have been transferred." : "Deine WoW-Accounts und Einstellungen wurden übernommen."
    };
    public string Information => Stage == "done" && !Fresh
        ? English ? "A new device entry may appear in the shared folder. Your playtime is still counted only once. The folder is checked during the first sync." : "Im gemeinsamen Ordner kann ein zusätzlicher Geräteeintrag erscheinen. Deine Spielzeit wird weiterhin nur einmal gezählt. Der Ordner wird beim ersten Abgleich geprüft."
        : English ? "Your existing data is kept. Synchronization starts only after setup is complete." : "Deine bisherigen Daten bleiben erhalten. Der Abgleich startet erst nach abgeschlossener Einrichtung.";
    public string PrimaryLabel => Stage switch
    {
        "found" => English ? "Transfer setup" : "Einrichtung übernehmen",
        "blocked" => English ? "Check again" : "Erneut prüfen",
        "prepare" => English ? "Transfer and switch" : "Übernehmen und wechseln",
        "fresh" => English ? "Continue without transfer" : "Ohne Übernahme fortfahren",
        "error" => English ? "Try again" : "Erneut versuchen",
        "running" => English ? "Please wait …" : "Bitte warten …",
        _ => English ? "Go to overview" : "Zur Übersicht"
    };
    public string SecondaryLabel => Stage == "found" ? English ? "Set up afresh" : "Neu einrichten" : English ? "Back" : "Zurück";
    public void SetState(string stage, bool english)
    {
        if (stage is not ("found" or "blocked" or "prepare" or "fresh" or "running" or "error" or "done")) throw new System.ArgumentException("Unknown setup stage.");
        Stage = stage; English = english; PropertyChanged?.Invoke(this, new(null));
    }
}
