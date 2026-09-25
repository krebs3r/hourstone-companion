using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hourstone.Companion.Core;

public sealed class CloudFileNotLocalException(string filePath) : IOException(
    "Cloud file is not stored locally. Mark the HourstoneSync folder as always available on this device.")
{
    public string FilePath { get; } = filePath;
}

public static class SafeFiles
{
    public static string StableRead(string path, int maximumBytes = SavedVariablesReader.MaximumFileBytes)
    {
        Exception? failure = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var before = new FileInfo(path); before.Refresh();
                if (!before.Exists) throw new FileNotFoundException("File is unavailable.", path);
                if (before.Length > maximumBytes) throw new InvalidDataException("File exceeds size limit.");
                EnsureLocallyAvailable(path, before.Attributes);
                var length = before.Length; var changedAt = before.LastWriteTimeUtc;
                byte[] bytes;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length != length) throw new IOException("File changed during read.");
                    bytes = new byte[checked((int)length)]; stream.ReadExactly(bytes);
                    if (stream.ReadByte() != -1) throw new IOException("File changed during read.");
                }
                // The next event/fallback scan retries a writer that has temporarily stopped in mid-file.
                before.Refresh();
                if (before.Length != length || before.LastWriteTimeUtc != changedAt) throw new IOException("File changed during read.");
                return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            }
            catch (IOException ex) when (ex is not CloudFileNotLocalException) { failure = ex; if (attempt < 2) Thread.Sleep(40); }
        }
        throw new IOException("Could not read a stable file: " + failure?.Message, failure);
    }
    internal static void EnsureLocallyAvailable(string path, FileAttributes attributes)
    {
        const FileAttributes notResident = FileAttributes.Offline | (FileAttributes)0x00400000 | (FileAttributes)0x00040000;
        if ((attributes & notResident) != 0) throw new CloudFileNotLocalException(path);
    }
    public static bool IsWithin(string candidate, string parent)
    {
        var full = Path.GetFullPath(candidate); var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return StringComparer.OrdinalIgnoreCase.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root) || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    public static void EnsureNoReparsePoints(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                // OneDrive files-on-demand use reparse points without redirecting to a different filesystem path.
                if (info.LinkTarget is not null) throw new IOException("Symbolic links and junctions are not supported for managed output paths.");
            }
            current = Path.GetDirectoryName(current);
        }
    }
    public static void AtomicWriteOwned(string directory, string fileName, string content)
    {
        if (Path.GetFileName(fileName) != fileName || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException("Invalid managed file name.");
        var targetDirectory = Path.GetFullPath(directory); EnsureNoReparsePoints(targetDirectory);
        Directory.CreateDirectory(targetDirectory);
        var destination = Path.Combine(targetDirectory, fileName); EnsureNoReparsePoints(destination);
        if (File.Exists(destination))
        {
            try { if (StableRead(destination) == content) return; }
            // Managed generated data is replaceable. Source files never use this writer.
            catch (Exception ex) when (ex is DecoderFallbackException or InvalidDataException) { }
        }
        var temporary = Path.Combine(targetDirectory, ".hourstone-" + System.Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = new UTF8Encoding(false, true).GetBytes(content); file.Write(bytes); file.Flush(true);
            }
            EnsureNoReparsePoints(destination);
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

public static partial class SourceDiscovery
{
    public static IReadOnlyList<DiscoveredSource> Discover(IEnumerable<string> roots, string deviceId)
    {
        var result = new List<DiscoveredSource>(); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawRoot in roots)
        {
            if (string.IsNullOrWhiteSpace(rawRoot)) continue;
            var root = Path.GetFullPath(rawRoot); if (!Directory.Exists(root)) continue;
            var clients = new List<string> { root };
            try { clients.AddRange(Directory.EnumerateDirectories(root).Where(p => Path.GetFileName(p).StartsWith('_'))); }
            catch (UnauthorizedAccessException) { continue; }
            foreach (var client in clients)
            {
                if (UnsupportedClient(client) || !visited.Add(client) || !Directory.Exists(Path.Combine(client, "WTF", "Account"))) continue;
                var region = DetectRegion(client);
                IEnumerable<string> accounts;
                try { accounts = Directory.GetDirectories(Path.Combine(client, "WTF", "Account")); }
                catch (UnauthorizedAccessException) { continue; }
                foreach (var account in accounts)
                {
                    var path = Path.Combine(account, "SavedVariables", "Hourstone.lua"); if (!File.Exists(path)) continue;
                    var flavor = DetectFlavor(client);
                    var provisional = new SourceConfiguration
                    {
                        SourceId = "pending-" + SafeFiles.Sha256(deviceId + "|" + Path.GetFullPath(path).ToUpperInvariant())[..24],
                        WoWRoot = root,
                        ClientDirectory = client,
                        AccountName = Path.GetFileName(account),
                        Region = region,
                        Flavor = flavor
                    };
                    try
                    {
                        // Flavor detection normally uses the client path; ambiguous custom installations try supported families.
                        var text = SafeFiles.StableRead(path); ParsedSavedVariables? parsed = null;
                        foreach (var candidateFlavor in new[] { flavor }.Concat(ObservationRules.Flavors).Distinct(StringComparer.Ordinal))
                        {
                            try
                            {
                                var candidate = SavedVariablesReader.Read(text, provisional with { Flavor = candidateFlavor });
                                if (parsed is null || candidate.Observations.Count > parsed.Observations.Count) { parsed = candidate; flavor = candidateFlavor; }
                            }
                            catch (InvalidDataException) { }
                        }
                        if (parsed is not null) provisional = provisional with { SourceId = parsed.SourceId ?? provisional.SourceId, Flavor = flavor };
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException) { }
                    result.Add(new DiscoveredSource { Configuration = provisional });
                }
            }
        }
        return result.OrderBy(s => s.ClientDirectory, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.AccountName, StringComparer.Ordinal).ToList();
    }
    public static void Validate(SourceConfiguration source)
    {
        if (UnsupportedClient(source.ClientDirectory)) throw new InvalidDataException("PTR, beta and test clients are not supported.");
        if (!ObservationRules.ValidSourceId(source.SourceId) || !ObservationRules.Flavors.Contains(source.Flavor, StringComparer.Ordinal) || !ObservationRules.Regions.Contains(source.Region, StringComparer.Ordinal))
            throw new InvalidDataException("Invalid source configuration.");
        if (string.IsNullOrWhiteSpace(source.WoWRoot) || string.IsNullOrWhiteSpace(source.ClientDirectory) || !Path.IsPathFullyQualified(source.WoWRoot) || !Path.IsPathFullyQualified(source.ClientDirectory) || !SafeFiles.IsWithin(source.ClientDirectory, source.WoWRoot))
            throw new InvalidDataException("Client path must remain within the selected WoW installation.");
        if (string.IsNullOrWhiteSpace(source.AccountName) || source.AccountName is "." or ".." || source.AccountName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || source.AccountName.Contains('/') || source.AccountName.Contains('\\'))
            throw new InvalidDataException("Invalid WoW account directory.");
        if (!SafeFiles.IsWithin(source.SavedVariablesPath, source.ClientDirectory) || !SafeFiles.IsWithin(source.DataAddonDirectory, source.ClientDirectory)) throw new InvalidDataException("Source path leaves selected client.");
    }
    private static bool UnsupportedClient(string client)
    {
        var name = Path.GetFileName(client.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.Contains("ptr", StringComparison.OrdinalIgnoreCase) || name.Contains("beta", StringComparison.OrdinalIgnoreCase)
            || name.Equals("_test_", StringComparison.OrdinalIgnoreCase) || name.Equals("_classic_test_", StringComparison.OrdinalIgnoreCase)
            || name.Equals("_alpha_", StringComparison.OrdinalIgnoreCase);
    }
    private static string DetectFlavor(string client) => Path.GetFileName(client).ToLowerInvariant() switch
    {
        "_retail_" => "retail",
        "_classic_era_" => "era",
        "_classic_anniversary_" or "_anniversary_" => "tbc",
        "_classic_" => "mists",
        _ => "retail"
    };
    private static string DetectRegion(string client)
    {
        var path = Path.Combine(client, "WTF", "Config.wtf"); if (!File.Exists(path)) return "unknown";
        try
        {
            var match = PortalRegex().Match(SafeFiles.StableRead(path, 1024 * 1024));
            var value = match.Success ? match.Groups[1].Value.ToLowerInvariant() : "unknown";
            return ObservationRules.Regions.Contains(value, StringComparer.Ordinal) ? value : "unknown";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException) { return "unknown"; }
    }
    [GeneratedRegex("(?im)^\\s*SET\\s+portal\\s+\"(us|kr|eu|tw|cn)\"\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PortalRegex();
}
