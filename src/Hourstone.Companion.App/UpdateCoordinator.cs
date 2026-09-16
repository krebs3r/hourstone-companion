using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
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
        ["Ready"] = ("App-Update heruntergeladen. Die Installation wartet auf ein geschlossenes WoW und ein unbenutztes App-Fenster.", "App update downloaded. Installation is waiting for WoW to close and the app window to be idle."),
        ["Unavailable"] = ("App-Updateprüfung momentan nicht verfügbar. Der lokale Abgleich läuft weiter.", "App update check unavailable. Local sync continues.")
    };
    public static string StatusMessage(string key, bool english) => english ? Messages[key].En : Messages[key].De;
    public static string LocalizeStatus(string message, bool english)
    {
        var key = Messages.FirstOrDefault(entry => entry.Value.De == message || entry.Value.En == message).Key;
        return key == null ? message : StatusMessage(key, english);
    }
    readonly UpdateManager manager = new(new GithubSource("https://github.com/krebs3r/hourstone-companion", null, false));
    readonly MainWindow window; readonly Action<string> notify;
    DateTimeOffset lastCheck = DateTimeOffset.MinValue, noticeAt = DateTimeOffset.MaxValue;
    VelopackAsset? pending; bool busy, announced;
    public bool IsInstalled => manager.IsInstalled;
    public bool SupportsAutostart => manager.IsInstalled && !manager.IsPortable;
    public string? InstallDirectory => Velopack.Locators.VelopackLocator.Current.RootAppDir;
    string T(string de, string en) => window.English ? en : de;
    public UpdateCoordinator(MainWindow window, Action<string> notify) { this.window = window; this.notify = notify; pending = manager.IsInstalled ? manager.UpdatePendingRestart : null; }
    void Announce() { if (announced) return; announced = true; noticeAt = DateTimeOffset.UtcNow; notify(T("Ein Update ist bereit. Installation erfolgt bei geschlossenem WoW und unbenutztem App-Fenster.", "An update is ready and will install when WoW is closed and this window is idle.")); }
    public async Task TickAsync()
    {
        if (busy || !IsInstalled) return;
        if (pending != null) Announce();
        if (pending == null && DateTimeOffset.UtcNow - lastCheck >= TimeSpan.FromDays(1)) await CheckAsync(false);
        if (pending != null && window.CanApplyUpdate(noticeAt, WoWRunning()))
        {
            busy = true; window.PrepareUpdate();
            try { manager.ApplyUpdatesAndRestart(pending, restartArgs: ["--background"]); }
            catch (Exception) { busy = false; pending = null; lastCheck = DateTimeOffset.UtcNow; window.ResumeAfterUpdateFailure(); notify(T("Das Update konnte nicht installiert werden. Der lokale Abgleich läuft weiter.", "The update could not be installed. Local sync continues.")); }
        }
    }
    public async Task<string> CheckAsync(bool manual)
    {
        if (busy) return StatusMessage("Busy", window.English);
        if (!IsInstalled) return StatusMessage("NotInstalled", window.English);
        busy = true; lastCheck = DateTimeOffset.UtcNow;
        try
        {
            var update = await manager.CheckForUpdatesAsync();
            if (update == null) return StatusMessage("NoNewerVersion", window.English);
            await manager.DownloadUpdatesAsync(update);
            pending = update.TargetFullRelease; announced = false; Announce();
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
