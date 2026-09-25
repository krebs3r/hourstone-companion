using System.Text.Json;
using Hourstone.Companion.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class ProgressIntegrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone.Progress.Tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<CompanionService> services = [];
    private CompanionService Service(string name)
    {
        var service = new CompanionService(Path.Combine(root, name, "state.db"), name); services.Add(service); return service;
    }
    private SourceConfiguration Configure(CompanionService service, string name, string capabilities = "4")
    {
        var source = Sample.Source("hs-" + name.ToLowerInvariant()) with
        { WoWRoot = Path.Combine(root, name, "WoW"), ClientDirectory = Path.Combine(root, name, "WoW", "_retail_") };
        Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!);
        File.WriteAllText(source.AddonTocPath, "## Version: 0.3.2\n## X-Hourstone-Sync-Protocol: " + capabilities + "\n");
        service.SaveConfiguration(service.GetConfiguration() with { Sources = [source] });
        return source;
    }
    private static void Save(SourceConfiguration source, string? families, double seconds = 120, string? owner = null, int progressVersion = 1)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!);
        var lua = Sample.Lua(source, Sample.Item(source.SourceId, seconds, "Player-1-A"), schema: 3);
        if (families is not null)
        {
            var progress = "progress={version=" + progressVersion + ",characters={one={sourceId=" + DataAddonWriter.Quote(owner ?? source.SourceId) +
                ",region=\"eu\",flavor=\"retail\",guid=\"Player-1-A\"," + families + "}}}, ";
            lua = lua.Replace("settings={", progress + "settings={", StringComparison.Ordinal);
        }
        File.WriteAllText(source.SavedVariablesPath, lua);
    }
    private static string Key(long updated, long level = 10) => $"keystone={{present=true,mapID=42,level={level},name=\"Dungeon\",updatedAt={updated},resetAt={updated + 100}}}";
    private static string Week(long updated, long level = 10, long season = 1) => $"weekly={{level={level},seasonID={season},updatedAt={updated},resetAt={updated + 100}}}";
    private static DeviceSnapshot Published(CompanionService service)
    {
        var config = service.GetConfiguration();
        return ObservationRules.ParseSnapshot(File.ReadAllText(Path.Combine(config.CloudFolder!, config.DeviceId + ".json")), config.GroupId!);
    }
    private async Task Connect(params CompanionService[] participants)
    {
        foreach (var service in participants) await service.JoinSyncFolderAsync(Path.Combine(root, "Cloud"));
        foreach (var service in participants) await service.SyncNowAsync();
        foreach (var service in participants) await service.SyncNowAsync();
    }
    [Fact]
    public async Task TwoPcRoundtripKeepsLocalProvenanceAndSeparatesPlaytimeFromProgressWinners()
    {
        var a = Service("A"); var b = Service("B"); var sourceA = Configure(a, "A"); var sourceB = Configure(b, "B");
        Save(sourceA, Key(110, 12) + "," + Week(100, 15), 500); Save(sourceB, Key(100, 8) + "," + Week(120, 16));
        var originalA = File.ReadAllBytes(sourceA.SavedVariablesPath); var originalB = File.ReadAllBytes(sourceB.SavedVariablesPath);
        await Connect(a, b);
        foreach (var service in new[] { a, b })
        {
            var item = Assert.Single(service.GetProgress()); Assert.Equal(12, item.Keystone!.Level); Assert.Equal(16, item.Weekly!.Level);
            Assert.Equal(500, Assert.Single(service.GetCharacters()).Seconds);
            var local = Assert.Single(Published(service).ProgressObservations!);
            Assert.Equal(service == a ? sourceA.SourceId : sourceB.SourceId, local.SourceId);
            Assert.Equal(service == a ? 15 : 16, local.Weekly!.Level);
        }
        var generated = File.ReadAllText(Path.Combine(sourceA.DataAddonDirectory, "Data.lua"));
        Assert.Contains("formatVersion = 4", generated); Assert.Contains("[\"sourceId\"]=\"hs-a\"", generated); Assert.Contains("[\"sourceId\"]=\"hs-b\"", generated);
        Assert.Equal(originalA, File.ReadAllBytes(sourceA.SavedVariablesPath)); Assert.Equal(originalB, File.ReadAllBytes(sourceB.SavedVariablesPath));
        var revision = Published(a).Revision; await a.SyncNowAsync(); Assert.Equal(revision, Published(a).Revision);
        Save(sourceA, "keystone={present=false,updatedAt=130,resetAt=230}," + Week(100, 15), 500);
        await a.SyncNowAsync(); await b.SyncNowAsync();
        Assert.False(Assert.Single(b.GetProgress()).Keystone!.Present); Assert.Equal(revision + 1, Published(a).Revision);
        Assert.Equal(8, Assert.Single(Published(b).ProgressObservations!).Keystone!.Level);
    }
    [Fact]
    public async Task PartialFamilyFailureFutureVersionAndMissingCachePreserveLastGoodProgressWithoutBlockingPlaytime()
    {
        var service = Service("A"); var source = Configure(service, "A");
        Save(source, Key(100) + "," + Week(100)); await service.SyncNowAsync();
        Save(source, "keystone={present=true,mapID=42,level=-1,updatedAt=110,resetAt=210}," + Week(120, 14), 250);
        var partial = await service.SyncNowAsync(); Assert.Contains(partial.Issues, issue => issue.Code == "progress_read_failed");
        var item = Assert.Single(service.GetProgress()); Assert.Equal(10, item.Keystone!.Level); Assert.Equal(14, item.Weekly!.Level);
        Assert.Equal(250, Assert.Single(service.GetCharacters()).Seconds);
        Save(source, Week(200, 30), 300, progressVersion: 99);
        var future = await service.SyncNowAsync(); Assert.Contains(future.Issues, issue => issue.Code == "progress_unsupported");
        Assert.Equal(14, Assert.Single(service.GetProgress()).Weekly!.Level); Assert.Equal(300, Assert.Single(service.GetCharacters()).Seconds);
        Save(source, null, 400); await service.SyncNowAsync(); Assert.Equal(14, Assert.Single(service.GetProgress()).Weekly!.Level);
        File.WriteAllText(source.SavedVariablesPath, "HourstoneDB = { partial");
        Assert.Contains((await service.SyncNowAsync()).Issues, issue => issue.Code == "source_read_failed");
        Assert.Equal(400, Assert.Single(service.GetCharacters()).Seconds); Assert.Equal(14, Assert.Single(service.GetProgress()).Weekly!.Level);
        service.Dispose(); services.Remove(service); service = Service("A");
        Assert.Equal(14, Assert.Single(service.GetProgress()).Weekly!.Level);
    }
    [Fact]
    public async Task ForeignSavedRecordsNeverBecomeLocalProgressAndSourceIdAdoptionDoesNotRelabelThem()
    {
        var service = Service("A"); var source = Configure(service, "A");
        var effective = source with { SourceId = "hs-game" };
        Save(effective, Key(100), owner: "hs-foreign"); await service.SyncNowAsync();
        Assert.Equal("hs-game", Assert.Single(service.GetConfiguration().Sources).SourceId); Assert.Empty(service.GetProgress());
        await service.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await service.SyncNowAsync(); Assert.Empty(Published(service).ProgressObservations!);
        Save(effective, Key(100)); await service.SyncNowAsync();
        Assert.Equal("hs-game", Assert.Single(Published(service).ProgressObservations!).SourceId);
    }
    [Fact]
    public async Task RemovedCharactersStillCarryProgressAndSelectionPauseDetachKeepTheirScopes()
    {
        var a = Service("A"); var b = Service("B"); var sourceA = Configure(a, "A"); var sourceB = Configure(b, "B");
        Save(sourceA, Key(100)); Save(sourceB, Week(120, 15)); await Connect(a, b);
        a.SetCharacterRemoved(Assert.Single(a.GetCharacters()), true); await a.SyncNowAsync(); await b.SyncNowAsync();
        Assert.Empty(b.GetCharacters()); Assert.Single(b.GetRemovedCharacters()); Assert.Equal(15, Assert.Single(b.GetProgress()).Weekly!.Level);
        Assert.Contains("progressObservations", File.ReadAllText(Path.Combine(sourceB.DataAddonDirectory, "Data.lua")));
        a.PauseCloud(true); Save(sourceB, Week(140, 20)); await b.SyncNowAsync(); await a.SyncNowAsync(); Assert.Equal(15, Assert.Single(a.GetProgress()).Weekly!.Level);
        a.PauseCloud(false); await a.SyncNowAsync(); Assert.Equal(20, Assert.Single(a.GetProgress()).Weekly!.Level);
        a.DetachSyncFolder(); var localOnly = Assert.Single(a.GetProgress()); Assert.Null(localOnly.Weekly); Assert.NotNull(localOnly.Keystone);
        b.SaveConfiguration(b.GetConfiguration() with { Sources = [] }); await b.SyncNowAsync(); Assert.Empty(Published(b).ProgressObservations!);
        Assert.DoesNotContain("hs-b\"] =", File.ReadAllText(Path.Combine(sourceB.DataAddonDirectory, "Data.lua")));
        b.DetachSyncFolder(); Assert.Empty(b.GetProgress());
    }
    [Theory]
    [InlineData("", 3)]
    [InlineData("3", 3)]
    [InlineData("4", 4)]
    [InlineData("5", 3)]
    [InlineData("garbage", 3)]
    [InlineData("4\n## X-Hourstone-Sync-Protocol: 4", 3)]
    public async Task OnlyOneExplicitKnownCapabilityEnablesProtocolFour(string capability, int expected)
    {
        var service = Service("A"); var source = Configure(service, "A", capability); Save(source, Key(100));
        var result = await service.SyncNowAsync(); Assert.Equal(expected, Assert.Single(result.LocalSourceStatuses).SyncFormatVersion);
        var generated = File.ReadAllText(Path.Combine(source.DataAddonDirectory, "Data.lua"));
        Assert.Contains($"formatVersion = {expected}", generated); Assert.Equal(expected == 4, generated.Contains("progressObservations", StringComparison.Ordinal));
        Assert.Equal(expected == 3, result.Issues.Any(issue => issue.Code == "progress_addon_update_required"));
        Assert.Single(service.GetProgress());
    }
    [Fact]
    public async Task CorruptRemoteProgressKeepsPeerRevisionAndDoesNotBlockHealthyPeers()
    {
        var service = Service("A"); var source = Configure(service, "A"); Save(source, Key(100));
        await service.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await service.SyncNowAsync();
        var config = service.GetConfiguration(); var peer = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-b", guid: "Player-1-A")) with
        { FormatVersion = 4, GroupId = config.GroupId!, Visibility = [], ProgressObservations = [ProgressTests.Item("hs-b", 110, 15)] };
        var path = Path.Combine(config.CloudFolder!, peer.DeviceId + ".json"); File.WriteAllText(path, ObservationRules.CanonicalSnapshot(peer));
        await service.SyncNowAsync(); Assert.Equal(15, Assert.Single(service.GetProgress()).Weekly!.Level);
        var malformed = ObservationRules.CanonicalSnapshot(peer with { Revision = 2 }).Replace("\"seasonID\": 1", "\"seasonID\": -1", StringComparison.Ordinal);
        File.WriteAllText(path, malformed);
        var healthy = peer with { DeviceId = "22222222-2222-2222-2222-222222222222", ProgressObservations = [ProgressTests.Item("hs-c", 120, 16)] };
        File.WriteAllText(Path.Combine(config.CloudFolder!, "healthy.json"), ObservationRules.CanonicalSnapshot(healthy));
        var result = await service.SyncNowAsync(); Assert.Contains(result.Issues, issue => issue.Code == "snapshot_rejected");
        Assert.Equal(16, Assert.Single(service.GetProgress()).Weekly!.Level); Assert.Equal(1, service.GetDevices().Single(d => d.DeviceId == peer.DeviceId).Revision);
        Assert.Equal("hs-a", Assert.Single(Published(service).ProgressObservations!).SourceId);
    }
    [Fact]
    public async Task AggregateProgressOverflowRetainsPeerCacheAndAllowsIndependentHealthyUpdate()
    {
        var service = Service("A"); var source = Configure(service, "A"); Save(source, Key(100));
        await service.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await service.SyncNowAsync(); var config = service.GetConfiguration();
        List<ProgressObservation> Many(int count) => Enumerable.Range(0, count).Select(i => new ProgressObservation
        { SourceId = "hs-many", Region = "eu", Flavor = "retail", Guid = "Player-extra-" + i }).ToList();
        var large = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1) with
        { FormatVersion = 4, GroupId = config.GroupId!, Visibility = [], ProgressObservations = Many(9998) };
        var healthy = Sample.Snapshot("22222222-2222-2222-2222-222222222222", 1, Sample.Item("hs-b", guid: "Player-1-A")) with
        { FormatVersion = 4, GroupId = config.GroupId!, Visibility = [], ProgressObservations = [ProgressTests.Item("hs-b", 110, 15)] };
        var largePath = Path.Combine(config.CloudFolder!, "large.json"); var healthyPath = Path.Combine(config.CloudFolder!, "healthy.json");
        File.WriteAllText(largePath, ObservationRules.CanonicalSnapshot(large)); File.WriteAllText(healthyPath, ObservationRules.CanonicalSnapshot(healthy));
        Assert.True((await service.SyncNowAsync()).Success);
        File.WriteAllText(largePath, ObservationRules.CanonicalSnapshot(large with { Revision = 2, ProgressObservations = Many(9999) }));
        File.WriteAllText(healthyPath, ObservationRules.CanonicalSnapshot(healthy with { Revision = 2, ProgressObservations = [ProgressTests.Item("hs-b", 120, 16)] }));
        var result = await service.SyncNowAsync(); Assert.Contains(result.Issues, issue => issue.Code == "snapshot_rejected" && issue.FilePath == largePath);
        Assert.Equal(1, service.GetDevices().Single(device => device.DeviceId == large.DeviceId).Revision);
        Assert.Equal(2, service.GetDevices().Single(device => device.DeviceId == healthy.DeviceId).Revision);
        Assert.Equal(16, Assert.Single(service.GetProgress()).Weekly!.Level);
    }
    [Fact]
    public async Task InvalidVaultRowPreservesOnlyThatRowWhileOtherRowsAdvance()
    {
        static string Row(int progress) => "{updatedAt=100,resetAt=200,slots={" + string.Join(',', Enumerable.Range(1, 3).Select(i =>
            $"{{progress={progress},threshold={i},level=10,unlocked=false}}")) + "}}";
        var service = Service("A"); var source = Configure(service, "A");
        Save(source, "vault={rows={dungeon=" + Row(2) + "}}"); await service.SyncNowAsync();
        Save(source, "vault={rows={dungeon={updatedAt=110,resetAt=200,slots={}},world=" + Row(3) + "}}");
        var result = await service.SyncNowAsync(); Assert.Contains(result.Issues, issue => issue.Code == "progress_read_failed");
        var rows = Assert.Single(service.GetProgress()).Vault!.Rows;
        Assert.Equal(2, rows.Dungeon!.Slots[0].Progress); Assert.Equal(3, rows.World!.Slots[2].Progress); Assert.True(rows.World.Slots[2].Unlocked); Assert.Null(rows.Raid);
    }
    [Fact]
    public void DatabaseOneMigratesAtomicallyAndNewerDatabaseIsRejected()
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "old.db");
        using (var connection = new SqliteConnection("Data Source=" + path))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL); INSERT INTO settings VALUES('preserved','yes'); PRAGMA user_version=1;"; command.ExecuteNonQuery();
        }
        using (var store = new CompanionStore(path))
        {
            Assert.Equal("yes", store.Get("preserved")); Assert.Empty(store.ReadSourceProgress("hs-a"));
            store.WriteSourceProgress("hs-a", [ProgressTests.Item()]); Assert.Single(store.ReadSourceProgress("hs-a"));
            store.RetainSources([]); Assert.Empty(store.ReadSourceProgress("hs-a"));
        }
        using (var connection = new SqliteConnection("Data Source=" + path))
        {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version;"; Assert.Equal(2L, command.ExecuteScalar());
            command.CommandText = "PRAGMA user_version=99;"; command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidDataException>(() => new CompanionStore(path));
    }
    public void Dispose()
    {
        foreach (var service in services) service.Dispose(); SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root); var temporary = Path.GetFullPath(Path.GetTempPath());
        if (full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.Progress.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
