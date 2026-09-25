using System.Text.Json;
using Hourstone.Companion.Core;

var root = Path.GetFullPath(args.Single());
const long epoch = 1800000000, reset = epoch + 1000;
const string guid = "Player-1-ROUNDTRIP";
var checks = new List<string>();
var cases = new List<object>();
void Check(bool condition, string name) { if (!condition) throw new InvalidDataException(name); checks.Add(name); }
string Q(string value) => DataAddonWriter.Quote(value);
string Lua(JsonElement value) => value.ValueKind switch
{
    JsonValueKind.Object => "{" + string.Join(',', value.EnumerateObject().Select(p => "[" + Q(p.Name) + "]=" + Lua(p.Value))) + "}",
    JsonValueKind.Array => "{" + string.Join(',', value.EnumerateArray().Select(Lua)) + "}",
    JsonValueKind.String => Q(value.GetString()!), JsonValueKind.True => "true", JsonValueKind.False => "false",
    JsonValueKind.Number => value.GetRawText(), _ => "nil"
};
string AsLua<T>(T value) => Lua(JsonSerializer.SerializeToElement(value, JsonContract.Options));
ProgressObservation Progress(string source, int key, int week, long keyAt, long weekAt, int dungeon, int raid, int world) => new()
{
    SourceId = source, Region = "eu", Flavor = "retail", Guid = guid,
    Keystone = new() { Present = true, MapID = 42, Name = "Synthetic Dungeon", Level = key, UpdatedAt = epoch + keyAt, ResetAt = reset },
    Weekly = new() { Level = week, SeasonID = 1, UpdatedAt = epoch + weekAt, ResetAt = reset },
    Vault = new() { Rows = new() { Dungeon = Row(dungeon, keyAt), Raid = Row(raid, weekAt), World = Row(world, weekAt) } }
};
VaultRowProgress Row(int count, long at, long? end = null) => new()
{
    UpdatedAt = epoch + at, ResetAt = end ?? reset,
    Slots = [.. new[] { 1, 4, 8 }.Select(n => new VaultSlot { Progress = count, Threshold = n, Level = 12, DifficultyName = "Mythic+" })]
};
SourceConfiguration Source(string pc) => new()
{
    SourceId = "hs-roundtrip-" + pc.ToLowerInvariant(), Region = "eu", Flavor = "retail", AccountName = "SYNTHETIC",
    WoWRoot = Path.Combine(root, "PC-" + pc, "WoW"), ClientDirectory = Path.Combine(root, "PC-" + pc, "WoW", "_retail_")
};
void Toc(SourceConfiguration source, bool modern)
{
    Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!);
    File.WriteAllText(source.AddonTocPath, modern ? "## Version: 0.3.2\n## X-Hourstone-Sync-Protocol: 4\n" : "## Version: 0.3.1\n");
}
Observation Played(SourceConfiguration source, int seconds) => new()
{
    SourceId = source.SourceId, Region = "eu", Flavor = "retail", Guid = guid, Name = "Synthetic Hero", Realm = "Synthetic Realm",
    Class = "MAGE", Level = 80, Seconds = seconds, UpdatedAt = epoch + 100, ServerSeconds = seconds, ServerAt = epoch + 90
};
void Save(SourceConfiguration source, ProgressObservation? progress, int seconds, int version = 1)
{
    Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!);
    var cache = progress is null ? "" : ",progress={version=" + version + ",characters={one=" + AsLua(progress) + "}}";
    File.WriteAllText(source.SavedVariablesPath, "-- Synthetic isolated verification data only\nHourstoneDB={version=3,sourceId=" + Q(source.SourceId) +
        ",visibility={},settings={},characters={one=" + AsLua(Played(source, seconds)) + "}" + cache + "}\n");
}
DeviceSnapshot Own(CompanionService service)
{
    var c = service.GetConfiguration(); return ObservationRules.ParseSnapshot(File.ReadAllText(Path.Combine(c.CloudFolder!, c.DeviceId + ".json")), c.GroupId!);
}
void Transport(CompanionService from, CompanionService to)
{
    var a = from.GetConfiguration(); var b = to.GetConfiguration();
    File.Copy(Path.Combine(a.CloudFolder!, a.DeviceId + ".json"), Path.Combine(b.CloudFolder!, a.DeviceId + ".json"), true);
}
async Task Scan(CompanionService service, params string[] allowedIssues)
{
    var result = await service.SyncNowAsync();
    Check(result.Issues.All(i => allowedIssues.Contains(i.Code)), "scan expected issues only: " + service.GetConfiguration().DeviceName + " [" + string.Join(',', result.Issues.Select(i => i.Code)) + "]");
}
void Capture(string name, CompanionService service, SourceConfiguration source, long now, ProgressObservation expected, bool legacy = false)
{
    var directory = Path.Combine(root, "checkpoints", name); Directory.CreateDirectory(directory);
    File.Copy(source.SavedVariablesPath, Path.Combine(directory, "SavedVariables.lua"));
    File.Copy(Path.Combine(source.DataAddonDirectory, "Data.lua"), Path.Combine(directory, "Data.lua"));
    var snapshot = Own(service); File.WriteAllText(Path.Combine(directory, "own-snapshot.json"), ObservationRules.CanonicalSnapshot(snapshot));
    Check(snapshot.ProgressObservations!.All(p => p.SourceId == source.SourceId), name + ": no foreign progress republished as local");
    cases.Add(new { name, directory, sourceId = source.SourceId, now, legacy, expected, seconds = service.GetCharacters().Single().Seconds });
}

Directory.CreateDirectory(root);
using var a = new CompanionService(Path.Combine(root, "PC-A", "profile", "companion.sqlite"), "Synthetic PC A");
using var b = new CompanionService(Path.Combine(root, "PC-B", "profile", "companion.sqlite"), "Synthetic PC B");
var sa = Source("A"); var sb = Source("B"); Toc(sa, false); Toc(sb, true);
a.SaveConfiguration(a.GetConfiguration() with { Sources = [sa] }); b.SaveConfiguration(b.GetConfiguration() with { Sources = [sb] });
var pa = Progress(sa.SourceId, 12, 15, 110, 100, 4, 2, 1); var pb = Progress(sb.SourceId, 8, 16, 100, 120, 2, 6, 3);
Save(sa, pa, 500); Save(sb, pb, 120);
var originalA = File.ReadAllBytes(sa.SavedVariablesPath); var originalB = File.ReadAllBytes(sb.SavedVariablesPath);
await a.JoinSyncFolderAsync(Path.Combine(root, "PC-A", "Cloud"));
var cloudB = Path.Combine(root, "PC-B", "Cloud", "HourstoneSync"); Directory.CreateDirectory(cloudB);
File.Copy(Path.Combine(a.GetConfiguration().CloudFolder!, "group.json"), Path.Combine(cloudB, "group.json"));
await b.JoinSyncFolderAsync(cloudB);
Check(a.GetConfiguration().CloudFolder != b.GetConfiguration().CloudFolder, "cloud directories are separate; transport copies bytes explicitly");
await Scan(a, "progress_addon_update_required"); await Scan(b); Transport(a, b); Transport(b, a);
await Scan(a, "progress_addon_update_required"); await Scan(b);
foreach (var service in new[] { a, b })
{
    var p = service.GetProgress().Single();
    Check(p.Keystone!.Level == 12 && p.Weekly!.Level == 16 && p.Vault!.Rows.Dungeon!.Slots[0].Progress == 4 && p.Vault.Rows.Raid!.Slots[0].Progress == 6 && p.Vault.Rows.World!.Slots[0].Progress == 3, "five independent family winners: " + service.GetConfiguration().DeviceName);
    Check(service.GetCharacters().Single().Seconds == 500, "playtime winner independent from progress: " + service.GetConfiguration().DeviceName);
}
Check(File.ReadAllBytes(sa.SavedVariablesPath).SequenceEqual(originalA) && File.ReadAllBytes(sb.SavedVariablesPath).SequenceEqual(originalB), "Companion never modifies either synthetic SavedVariables file");
Capture("01-legacy-addon-031", a, sa, epoch + 150, pa, legacy: true);
Toc(sa, true); await Scan(a);
Capture("02-protocol4-A", a, sa, epoch + 150, a.GetProgress().Single());
Capture("03-protocol4-B", b, sb, epoch + 150, b.GetProgress().Single());
var revision = Own(a).Revision; await Scan(a); Check(Own(a).Revision == revision, "unchanged rescan retains publication revision");

pb = pb with { Keystone = pb.Keystone! with { Level = 2, UpdatedAt = epoch + 200 } }; Save(sb, pb, 120);
await Scan(b); Transport(b, a); await Scan(a);
Check(a.GetProgress().Single().Keystone!.Level == 2, "newer lower keystone replaces older higher key");
Capture("04-lower-keystone", a, sa, epoch + 220, a.GetProgress().Single());
pa = pa with { Keystone = new() { Present = false, UpdatedAt = epoch + 250, ResetAt = reset } }; Save(sa, pa, 500);
await Scan(a); Transport(a, b); await Scan(b);
Check(!b.GetProgress().Single().Keystone!.Present, "confirmed key absence survives transport");
Capture("05-no-keystone", b, sb, epoch + 270, b.GetProgress().Single());
Capture("06-week-expired", b, sb, reset + 1, b.GetProgress().Single());
Check(ProgressRules.State(b.GetProgress().Single().Weekly, DateTimeOffset.FromUnixTimeSeconds(reset + 1)) == ProgressState.Stale, "weekly state becomes stale after reset boundary");

pa = pa with { Keystone = new() { Present = false, UpdatedAt = reset + 20, ResetAt = reset + 604800 },
    Weekly = new() { Level = 0, SeasonID = 1, UpdatedAt = reset + 20, ResetAt = reset + 604800 },
    Vault = new() { Rows = new() { Dungeon = Row(0, 1020, reset + 604800), Raid = Row(0, 1020, reset + 604800), World = Row(0, 1020, reset + 604800) } } };
Save(sa, pa, 500); await Scan(a); Transport(a, b); await Scan(b);
Check(b.GetProgress().Single().Weekly!.Level == 0 && b.GetProgress().Single().Vault!.Rows.Raid!.Slots[0].Progress == 0, "new-week zeros replace old weekly and vault results");
Capture("07-new-week-empty", b, sb, reset + 30, b.GetProgress().Single());

var accepted = b.GetDevices().Single(d => d.DeviceId == a.GetConfiguration().DeviceId).Revision;
var damaged = ObservationRules.CanonicalSnapshot(Own(a) with { Revision = accepted + 10 }).Replace("\"seasonID\": 1", "\"seasonID\": -1", StringComparison.Ordinal);
File.WriteAllText(Path.Combine(b.GetConfiguration().CloudFolder!, a.GetConfiguration().DeviceId + ".json"), damaged);
var damagedResult = await b.SyncNowAsync();
Check(damagedResult.Issues.Any(i => i.Code == "snapshot_rejected"), "damaged remote progress snapshot is diagnosed");
Check(b.GetDevices().Single(d => d.DeviceId == a.GetConfiguration().DeviceId).Revision == accepted && b.GetProgress().Single().Weekly!.Level == 0, "damaged snapshot retains last accepted remote revision and progress");
Capture("08-damaged-peer-preserved", b, sb, reset + 30, b.GetProgress().Single());
Transport(a, b);

Save(sa, pa with { Weekly = pa.Weekly! with { Level = 99 } }, 600, version: 99);
var unsupported = await a.SyncNowAsync();
Check(unsupported.Issues.Any(i => i.Code == "progress_unsupported") && a.GetProgress().Single().Weekly!.Level == 0 && a.GetCharacters().Single().Seconds == 600, "future optional cache preserves progress while playtime advances");
Transport(a, b); await Scan(b);
Capture("09-future-local-cache", a, sa, reset + 30, a.GetProgress().Single());
Save(sb, null, 120); await Scan(b);
Capture("10-missing-local-cache", b, sb, reset + 30, b.GetProgress().Single());

var manifest = new { passed = true, scope = "Two isolated synthetic profiles on one machine; file-copy cloud transport; no real WoW APIs or physical second PC.", checks, cases };
File.WriteAllText(Path.Combine(root, "companion-report.json"), JsonSerializer.Serialize(manifest, JsonContract.Options));
Console.WriteLine($"PASS Companion roundtrip: {checks.Count} checks, {cases.Count} generated-addon checkpoints.");
