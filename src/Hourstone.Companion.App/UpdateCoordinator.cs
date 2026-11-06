using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
namespace Hourstone.Companion.App;

public sealed class UpdateCoordinator
{
    static readonly Dictionary<string, (string De, string En)> Messages = new()
    {
        ["Busy"] = ("Eine App-Updateprüfung läuft bereits.", "An app update check is already running."),
        ["NotInstalled"] = ("App-Updates stehen nach Installation zur Verfügung.", "Install the application to enable app updates."),
        ["NoNewerVersion"] = ("Keine neuere App-Version verfügbar.", "No newer app version is available."),
        ["Ready"] = ($"App-Update heruntergeladen. Es wird automatisch installiert, sobald WoW geschlossen ist und die App {UpdatePolicy.IdleSeconds} Sekunden lang unbenutzt im Hintergrund liegt – oder sofort mit „Jetzt installieren“.", $"App update downloaded. It installs automatically once WoW is closed and the app has been left idle in the background for {UpdatePolicy.IdleSeconds} seconds, or right away with Install now."),
        ["Unavailable"] = ("App-Updateprüfung momentan nicht verfügbar. Der lokale Abgleich läuft weiter.", "App update check unavailable. Local sync continues."),
        ["Store"] = ("Neue Versionen erhältst du über den Microsoft Store.", "New versions are available through Microsoft Store."),
        ["SaveSettingsFirst"] = ("Speichere zuerst deine geänderten Einstellungen oder mache die Änderungen rückgängig. Wähle danach erneut „Jetzt installieren“.", "Save your changed settings or undo the changes first, then choose Install now again."),
        ["InstallAfterSync"] = ("Die Installation startet, sobald der laufende Abgleich abgeschlossen ist.", "The installation starts as soon as the running sync has finished.")
    };
    public static string StatusMessage(string key, bool english) => english ? Messages[key].En : Messages[key].De;
    public static string LocalizeStatus(string message, bool english)
    {
        var key = Messages.FirstOrDefault(entry => entry.Value.De == message || entry.Value.En == message).Key;
        return key == null ? message : StatusMessage(key, english);
    }
    /// <summary>The Ready status points at Install now; once nothing is pending any more (e.g. after a failed install) it is cleared.</summary>
    public static string ClearStaleReady(string message, bool pending) => pending || (Messages["Ready"].De != message && Messages["Ready"].En != message) ? message : "";
    public static string ReadyNotice(string version, bool english) => string.Format(CultureInfo.InvariantCulture, english
        ? "Update {0} is ready. It installs automatically once WoW is closed and the app has been left idle in the background for {1} seconds, or right away with Install now."
        : "Update {0} ist bereit. Es wird automatisch installiert, sobald WoW geschlossen ist und die App {1} Sekunden lang unbenutzt im Hintergrund liegt – oder sofort mit „Jetzt installieren“.", version, UpdatePolicy.IdleSeconds);
    readonly UpdateManager? manager;
    readonly MainWindow window; readonly Action<string> notify;
    DateTimeOffset lastCheck = DateTimeOffset.MinValue, noticeAt = DateTimeOffset.MaxValue;
    VelopackAsset? pending; bool busy, announced;
    public bool IsInstalled => !AppRuntime.IsSmokeTest && (AppDistribution.Current.IsStore || manager?.IsInstalled == true);
    public bool SupportsAutostart => !AppRuntime.IsSmokeTest && (AppDistribution.Current.IsStore || (manager?.IsInstalled == true && !manager.IsPortable));
    public string? InstallDirectory => AppDistribution.Current.InstallDirectory;
    /// <summary>The downloaded direct-channel version waiting for installation; never set for Store or uninstalled runs.</summary>
    public string? PendingVersion => manager != null && IsInstalled ? pending?.Version.ToString() : null;
    /// <summary>An explicit request that waits for the running sync.</summary>
    public bool InstallRequested { get; private set; }
    string T(string de, string en) => window.English ? en : de;
    public UpdateCoordinator(MainWindow window, Action<string> notify)
    {
        this.window = window; this.notify = notify;
        if (!AppDistribution.Current.IsStore)
        {
            manager = new(new GithubSource("https://github.com/krebs3r/hourstone-companion", null, false));
            pending = manager.IsInstalled ? manager.UpdatePendingRestart : null;
        }
    }
    void Announce() { if (announced) return; announced = true; noticeAt = DateTimeOffset.UtcNow; window.ShowUpdateReady(); }
    public async Task TickAsync()
    {
        if (manager is null || busy || !IsInstalled) return;
        if (pending != null && InstallRequested)
        {
            var outcome = window.ManualInstall(false);
            if (outcome == ManualInstallOutcome.Install) { Apply(false); return; }
            if (outcome == ManualInstallOutcome.UnsavedSettings) { InstallRequested = false; window.RefreshUpdateActions(); notify(StatusMessage("SaveSettingsFirst", window.English)); }
        }
        if (pending != null) Announce();
        if (pending == null && DateTimeOffset.UtcNow - lastCheck >= TimeSpan.FromDays(1)) await CheckAsync(false);
        if (pending != null && window.CanApplyUpdate(noticeAt, WoWRunning())) Apply(true);
    }
    /// <summary>Installs the pending update on the user's explicit request. Returns feedback when it does not start now.</summary>
    public string? InstallNow()
    {
        if (manager is null || !IsInstalled || pending == null) return null;
        switch (window.ManualInstall(busy))
        {
            case ManualInstallOutcome.Install: Apply(false); return null;
            case ManualInstallOutcome.AfterSync: InstallRequested = true; window.RefreshUpdateActions(); return StatusMessage("InstallAfterSync", window.English);
            case ManualInstallOutcome.CheckRunning: return StatusMessage("Busy", window.English);
            case ManualInstallOutcome.UnsavedSettings: return StatusMessage("SaveSettingsFirst", window.English);
            default: return null;
        }
    }
    // An automatic install restarts in the background; an explicit one restarts visibly because the window is in use.
    void Apply(bool automatic)
    {
        if (manager is null || pending is null) return;
        busy = true; InstallRequested = false; window.PrepareUpdate();
        try { manager.ApplyUpdatesAndRestart(pending, restartArgs: automatic ? ["--background"] : []); }
        catch (Exception) { busy = false; pending = null; lastCheck = DateTimeOffset.UtcNow; window.ResumeAfterUpdateFailure(); notify(T("Das Update konnte nicht installiert werden. Der lokale Abgleich läuft weiter.", "The update could not be installed. Local sync continues.")); }
    }
    public async Task<string> CheckAsync(bool manual)
    {
        if (manager is null)
        {
            if (manual) AppDistribution.Current.OpenStore();
            return StatusMessage("Store", window.English);
        }
        if (busy) return StatusMessage("Busy", window.English);
        if (!IsInstalled) return StatusMessage("NotInstalled", window.English);
        busy = true; lastCheck = DateTimeOffset.UtcNow;
        try
        {
            var update = await manager.CheckForUpdatesAsync();
            if (update == null) return StatusMessage("NoNewerVersion", window.English);
            // The downloaded version stays announced; a repeated check neither shows a second notice nor restarts the delay.
            if (!UpdatePolicy.ReplacesPending(pending?.Version.ToString(), update.TargetFullRelease.Version.ToString())) return StatusMessage("Ready", window.English);
            await manager.DownloadUpdatesAsync(update);
            pending = update.TargetFullRelease; announced = false; window.RefreshUpdateActions(); Announce();
            return StatusMessage("Ready", window.English);
        }
        catch (Exception)
        {
            var message = StatusMessage("Unavailable", window.English);
            if (manual) notify(message); return message;
        }
        finally { busy = false; }
    }
    static bool WoWRunning()
    {
        try
        {
            var processes = Process.GetProcesses();
            try { foreach (var process in processes) { try { if (process.ProcessName.StartsWith("Wow", StringComparison.OrdinalIgnoreCase)) return true; } catch (InvalidOperationException) { } catch (Win32Exception) { return true; } } }
            finally { foreach (var process in processes) process.Dispose(); }
            return false;
        }
        catch (Win32Exception) { return true; }
    }
}
