using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Hourstone.Companion.App;

public enum AppStartupState { Unavailable, Disabled, Enabled, DisabledByUser, DisabledByPolicy }
public static class StartupService
{
    public const string TaskId = "HourstoneCompanionStartup";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValue = "HourstoneCompanion";
    public static async Task<AppStartupState> GetStateAsync()
    {
        if (AppDistribution.Current.IsStore) return Map((await StartupTask.GetAsync(TaskId)).State);
        if (AppDistribution.Current.Kind != DistributionKind.VelopackInstalled) return AppStartupState.Unavailable;
        return ReadLegacyAutostart() is null ? AppStartupState.Disabled : AppStartupState.Enabled;
    }
    public static async Task<AppStartupState> SetEnabledAsync(bool enabled)
    {
        if (AppDistribution.Current.IsStore)
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (!enabled) { task.Disable(); return Map(task.State); }
            if (task.State is StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy) return Map(task.State);
            return Map(await task.RequestEnableAsync());
        }
        if (AppDistribution.Current.Kind != DistributionKind.VelopackInstalled) return AppStartupState.Unavailable;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enabled) { key.DeleteValue(RunValue, false); return AppStartupState.Disabled; }
        var stub = GetDirectLauncherPath(AppDistribution.Current.InstallDirectory);
        key.SetValue(RunValue, "\"" + stub + "\" --background");
        return AppStartupState.Enabled;
    }
    public static string GetDirectLauncherPath(string? installationDirectory)
    {
        if (string.IsNullOrWhiteSpace(installationDirectory)) throw new IOException("Installation directory unavailable.");
        var root = Path.GetFullPath(installationDirectory);
        // Velopack names the root launcher after the package title; older packages used the assembly name.
        foreach (var name in new[] { "Hourstone Companion.exe", "Hourstone.Companion.exe" })
        {
            var launcher = Path.Combine(root, name);
            if (File.Exists(launcher)) return launcher;
        }
        throw new FileNotFoundException("Installed startup launcher is missing.", Path.Combine(root, "Hourstone Companion.exe"));
    }
    public static AppStartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => AppStartupState.Enabled,
        StartupTaskState.DisabledByUser => AppStartupState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => AppStartupState.DisabledByPolicy,
        _ => AppStartupState.Disabled
    };
    public static string? ReadLegacyAutostart()
    { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(RunValue) as string; }
    // A Run entry may remain present when Windows has disabled it. This is guidance, not an activation gate.
    internal static bool NeedsLegacyHandover(StoreImportCandidate candidate, Func<string?>? readStartup = null)
    {
        if (candidate.Available || candidate.Error is not null) return true;
        try { return (readStartup ?? ReadLegacyAutostart)() is not null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return true; }
    }
    public static BrowserLaunchResult OpenWindowsSettings() => ProductLinks.OpenAddress("ms-settings:startupapps");
}
