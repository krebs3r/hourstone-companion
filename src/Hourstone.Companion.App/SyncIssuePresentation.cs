using System.Linq;
using Hourstone.Companion.Core;

namespace Hourstone.Companion.App;

/// <summary>Localized sync diagnostics and notices; these messages are not part of the sync protocol.</summary>
public static class SyncIssuePresentation
{
    public static string? Diagnostics(SyncIssue issue, bool english, bool joiningSyncFolder = false)
    {
        var file = string.IsNullOrWhiteSpace(issue.FilePath)
            ? (english ? "The file" : "Die Datei")
            : (english ? $"The file “{issue.FilePath}”" : $"Die Datei „{issue.FilePath}“");
        return issue.Code switch
        {
            "cloud_file_not_local" => $"{issue.Code}: {file} " + (english
                ? "is not yet fully available locally. "
                : "ist noch nicht vollständig lokal verfügbar. ") + OfflineInstruction(english) + " " + (joiningSyncFolder
                    ? (english ? "Then select the sync folder again. Last valid data is kept." : "Wähle den Sync-Ordner anschließend erneut aus. Gültige Daten bleiben erhalten.")
                    : RetryInstruction(english)),
            "snapshot_read_failed" => $"{issue.Code}: {file} " + (english
                ? "could not be read. Check file access and whether another program is locking it. "
                : "konnte nicht gelesen werden. Prüfe den Dateizugriff und ob ein anderes Programm die Datei sperrt. ") + RetryInstruction(english) + " " + issue.Message,
            "snapshot_rejected" => $"{issue.Code}: {file} " + (english
                ? "contains an invalid or unsupported device snapshot and was not imported. Last valid data is kept. "
                : "enthält einen ungültigen oder nicht unterstützten Geräte-Snapshot und wurde nicht übernommen. Gültige Daten bleiben erhalten. ") + issue.Message,
            _ => null
        };
    }

    public static string? Notice(SyncResult result, bool english)
    {
        if (result.Success) return null;
        var pending = result.LocalSourceStatuses.Count(status => status.Readiness != LocalSourceReadiness.Ready);
        if (pending > 0) return english
            ? $"{pending} account source(s) need attention. Open Clients for the next step. Last valid data is kept."
            : $"{pending} Account-Quelle(n) benötigen Aufmerksamkeit. Unter Clients findest du den nächsten Schritt. Gültige Daten bleiben erhalten.";
        if (result.Issues.Any(issue => issue.Code == "cloud_file_not_local")) return (english
            ? "A sync file is not yet fully available locally. "
            : "Eine Sync-Datei ist noch nicht vollständig lokal verfügbar. ") + OfflineInstruction(english) + (english
                ? " See Synchronization → Local diagnostics for the affected file. Last valid data is kept."
                : " Die betroffene Datei steht unter Synchronisierung → Lokale Diagnose. Gültige Daten bleiben erhalten.");
        return english
            ? "Sync needs attention. Last valid data is kept. See Synchronization → Local diagnostics."
            : "Der Abgleich benötigt Aufmerksamkeit. Gültige Daten bleiben erhalten. Details stehen unter Synchronisierung → Lokale Diagnose.";
    }

    private static string OfflineInstruction(bool english) => english
        ? "Set the entire sync folder (for example, HourstoneSync) to “Always keep on this device” on both PCs and wait for the download."
        : "Setze den gesamten Sync-Ordner (z. B. HourstoneSync) auf beiden PCs auf „Immer auf diesem Gerät behalten“ und warte auf den Download.";

    private static string RetryInstruction(bool english) => english
        ? "Last valid data is kept. The Companion will retry automatically."
        : "Gültige Daten bleiben erhalten. Der Companion versucht das Lesen automatisch erneut.";
}
