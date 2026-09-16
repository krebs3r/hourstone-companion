using System.Text;

namespace Hourstone.Companion.Core;

/// <summary>Reads only bounded TOC metadata; it never executes addon code or writes WoW files.</summary>
public static class AddonReadiness
{
    public const string MinimumAddonVersion = "0.2.0";
    public const int MaximumTocBytes = 64 * 1024;
    public static LocalSourceStatus Inspect(SourceConfiguration source)
    {
        var status = new LocalSourceStatus(source.SourceId, source.ClientDirectory, source.AccountName,
            source.Flavor, LocalSourceReadiness.Ready, null);
        var context = $"{source.Flavor} / {source.AccountName}: ";
        try
        {
            if (!File.Exists(source.AddonTocPath)) return status with
            {
                Readiness = LocalSourceReadiness.AddonMissing,
                Message = context + $"Das Hourstone-Addon fehlt. Installiere Hourstone {MinimumAddonVersion} oder neuer in dieser WoW-Installation."
            };
            var text = SafeFiles.StableRead(source.AddonTocPath, MaximumTocBytes);
            string? detected = null;
            foreach (var line in text.Split('\n'))
            {
                var metadata = line.Trim();
                if (!metadata.StartsWith("##", StringComparison.Ordinal)) continue;
                metadata = metadata[2..].TrimStart(); var colon = metadata.IndexOf(':');
                if (colon < 0 || !metadata[..colon].Trim().Equals("Version", StringComparison.OrdinalIgnoreCase)) continue;
                if (detected is not null) throw new InvalidDataException("Duplicate addon Version metadata.");
                detected = metadata[(colon + 1)..].Trim();
                if (detected.Length > 128 || detected.Any(char.IsControl)) throw new InvalidDataException("Invalid addon Version metadata.");
            }
            status = status with { DetectedAddonVersion = string.IsNullOrEmpty(detected) ? null : detected };
            var numeric = detected?.TrimStart('v', 'V');
            var supported = Version.TryParse(numeric, out var version) && version >= new Version(0, 2, 0);
            if (!supported) return status with
            {
                Readiness = LocalSourceReadiness.AddonOutdated,
                Message = context + $"Hourstone {detected ?? "(Version unbekannt)"} ist installiert. Aktualisiere das WoW-Addon auf mindestens {MinimumAddonVersion}."
            };
            return status;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return status with { Readiness = LocalSourceReadiness.ReadFailed,
                Message = context + "Die Addon-Version konnte nicht gelesen werden: " + ex.Message };
        }
    }
    internal static string AwaitingSaveMessage(SourceConfiguration source) =>
        $"{source.Flavor} / {source.AccountName}: Lade Hourstone {MinimumAddonVersion} oder neuer in diesem WoW-Client und logge dich mit diesem Account ein. Logge dich danach aus oder führe /reload in WoW aus, damit die Quelldaten gespeichert werden. Ein Neustart des Companions ersetzt diesen Schritt nicht.";
}
