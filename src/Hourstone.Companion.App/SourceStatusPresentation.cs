using System;
using System.Collections.Generic;
using System.Linq;
using Hourstone.Companion.Core;

namespace Hourstone.Companion.App;

/// <summary>Presentation of local readiness only; none of this text is part of the sync protocol.</summary>
public static class SourceStatusPresentation
{
    public static string Title(LocalSourceStatus status, bool english) => status.Readiness switch
    {
        LocalSourceReadiness.AddonMissing => english ? "Hourstone missing" : "Hourstone fehlt",
        LocalSourceReadiness.AddonOutdated => english ? "Update Hourstone" : "Hourstone aktualisieren",
        LocalSourceReadiness.AwaitingGameSave => english ? "Waiting for a WoW save" : "Warte auf Speicherung in WoW",
        LocalSourceReadiness.Ready => english ? "Ready" : "Bereit",
        LocalSourceReadiness.ReadFailed => english ? "Read failed" : "Lesen fehlgeschlagen",
        _ => english ? "Unknown status" : "Status unbekannt"
    };

    public static string Instruction(LocalSourceStatus status, bool english)
    {
        var minimum = AddonReadiness.MinimumAddonVersion;
        var save = english
            ? "Enable the addon in this WoW client, log in with this account, then log out or use /reload in WoW to save its data. Restarting the Companion does not replace this step."
            : "Aktiviere das Addon in diesem WoW-Client, logge dich mit diesem Account ein und anschließend aus oder nutze /reload in WoW, um die Daten zu speichern. Ein Neustart des Companions ersetzt diesen Schritt nicht.";
        return status.Readiness switch
        {
            LocalSourceReadiness.AddonMissing => (english
                ? $"Install the Hourstone WoW addon {minimum} or later in this client. "
                : $"Installiere das WoW-Addon Hourstone {minimum} oder neuer in diesem Client. ") + save,
            LocalSourceReadiness.AddonOutdated => (english
                ? $"Installed version: {VersionLabel(status, true)}. Update the Hourstone WoW addon to {minimum} or later. "
                : $"Installierte Version: {VersionLabel(status, false)}. Aktualisiere das WoW-Addon Hourstone auf {minimum} oder neuer. ") + save,
            LocalSourceReadiness.AwaitingGameSave => (english
                ? $"Hourstone {minimum} or later must save this account's data once in WoW. "
                : $"Hourstone {minimum} oder neuer muss die Daten dieses Accounts einmal in WoW speichern. ") + save,
            LocalSourceReadiness.Ready => english
                ? $"Detected Hourstone version: {VersionLabel(status, true)}. This account's saved data is ready to sync."
                : $"Erkannte Hourstone-Version: {VersionLabel(status, false)}. Die gespeicherten Daten dieses Accounts sind für den Abgleich bereit.",
            LocalSourceReadiness.ReadFailed => english
                ? "The last valid data is kept. Check the local files and their availability; the Companion will retry the read."
                : "Die letzten gültigen Daten bleiben erhalten. Prüfe die lokalen Dateien und ihre Verfügbarkeit; der Companion versucht das Lesen erneut.",
            _ => english ? "Check the local diagnostics." : "Prüfe die lokale Diagnose."
        };
    }

    public static string Diagnostics(SyncResult result, IReadOnlyList<SourceConfiguration> sources, bool english)
    {
        var lines = new List<string>();
        var coveredSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issue in result.Issues)
        {
            var status = result.LocalSourceStatuses.FirstOrDefault(item => issue.SourceId is not null && item.SourceId == issue.SourceId);
            var source = sources.FirstOrDefault(item => issue.SourceId is not null && item.SourceId == issue.SourceId);
            if (status is null && source is not null)
                status = new LocalSourceStatus(source.SourceId, source.ClientDirectory, source.AccountName, source.Flavor,
                    ReadinessForCode(issue.Code) ?? LocalSourceReadiness.ReadFailed, null);
            if (status is null)
            {
                // Cloud issues and unidentified sources keep their diagnostic code and detail.
                lines.Add($"{issue.Code}: {issue.Message}");
                continue;
            }
            coveredSources.Add(status.SourceId);
            var context = Context(status);
            var readiness = ReadinessForCode(issue.Code);
            if (readiness is not null)
            {
                var described = status with { Readiness = readiness.Value };
                var line = $"{context}: {Title(described, english)}. {Instruction(described, english)} [{issue.Code}]";
                if (readiness == LocalSourceReadiness.ReadFailed) line += " " + issue.Message;
                lines.Add(line);
            }
            else lines.Add($"{context}: [{issue.Code}] {issue.Message}");
        }
        foreach (var status in result.LocalSourceStatuses)
        {
            if (coveredSources.Contains(status.SourceId)) continue;
            var line = $"{Context(status)}: {Title(status, english)}. {Instruction(status, english)}";
            if (status.Readiness == LocalSourceReadiness.ReadFailed && !string.IsNullOrWhiteSpace(status.Message)) line += " " + status.Message;
            lines.Add(line);
        }
        if (lines.Count > 0) return string.Join(Environment.NewLine, lines);
        if (!sources.Any(source => source.Enabled)) return english ? "No local clients configured yet." : "Noch keine lokalen Clients eingerichtet.";
        if (result.CompletedAt == DateTimeOffset.MinValue) return english ? "No check has run yet." : "Es wurde noch keine Prüfung durchgeführt.";
        return english ? "Last check completed. No errors." : "Letzte Prüfung abgeschlossen. Keine Fehler.";
    }

    private static string Context(LocalSourceStatus status) => $"{MainViewModel.ClientName(status.Flavor)} · {status.AccountName}";
    private static string VersionLabel(LocalSourceStatus status, bool english) => string.IsNullOrWhiteSpace(status.DetectedAddonVersion)
        ? english ? "unknown" : "unbekannt" : status.DetectedAddonVersion;
    private static LocalSourceReadiness? ReadinessForCode(string code) => code switch
    {
        "addon_missing" => LocalSourceReadiness.AddonMissing,
        "addon_outdated" => LocalSourceReadiness.AddonOutdated,
        "source_not_initialized" => LocalSourceReadiness.AwaitingGameSave,
        "source_read_failed" or "addon_read_failed" => LocalSourceReadiness.ReadFailed,
        _ => null
    };
}
