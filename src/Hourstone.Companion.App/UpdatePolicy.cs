using System;
namespace Hourstone.Companion.App;

/// <summary>Why an explicit "Install now" request starts, waits for the running sync or is refused.</summary>
public enum ManualInstallOutcome { Install, AfterSync, CheckRunning, ModalOpen, UnsavedSettings }

public static class UpdatePolicy
{
    /// <summary>Idle time in the background before an automatic install; the update texts quote the same value.</summary>
    public const int IdleSeconds = 90;
    public static bool CanApply(bool syncBusy, bool windowVisible, bool windowActive, bool modalOpen, DateTimeOffset lastInteraction, DateTimeOffset noticeAt, DateTimeOffset now, bool wowRunning)
        => !syncBusy && !modalOpen && (!windowVisible || !windowActive) && !wowRunning
        && now - lastInteraction > TimeSpan.FromSeconds(IdleSeconds) && now - noticeAt > TimeSpan.FromSeconds(30);
    /// <summary>An explicit request ignores WoW, idle time, window focus and the notice delay. It never discards a settings
    /// draft or interrupts an update check or dialog, and it waits for a running sync instead of refusing.</summary>
    public static ManualInstallOutcome ManualInstall(bool syncBusy, bool savingSettings, bool settingsDirty, bool modalOpen, bool checkBusy)
        => checkBusy ? ManualInstallOutcome.CheckRunning : modalOpen ? ManualInstallOutcome.ModalOpen
        : settingsDirty || savingSettings ? ManualInstallOutcome.UnsavedSettings : syncBusy ? ManualInstallOutcome.AfterSync : ManualInstallOutcome.Install;
    /// <summary>A check that finds the already downloaded version keeps its notice and delay; only another version replaces it.</summary>
    public static bool ReplacesPending(string? pendingVersion, string foundVersion) => !string.Equals(pendingVersion, foundVersion, StringComparison.OrdinalIgnoreCase);
}
