using System.Text.Json;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class SyncIntegrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone.Companion.Tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<CompanionService> services = [];
    private CompanionService Service(string device)
    {
        var service = new CompanionService(Path.Combine(root, device, "app", "state.db"), device); services.Add(service); return service;
    }
    private SourceConfiguration Configure(CompanionService service, string device, string sourceId, double seconds = 120, string guid = "Player-1-AB")
    {
        var client = Path.Combine(root, device, "WoW", "_retail_");
        var source = Sample.Source(sourceId) with { WoWRoot = Path.Combine(root, device, "WoW"), ClientDirectory = client };
        Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!); File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, Sample.Item(sourceId, seconds, guid)));
        Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!); File.WriteAllText(source.AddonTocPath, "## Version: 0.2.0\n");
        service.SaveConfiguration(service.GetConfiguration() with { Sources = [source] }); return source;
    }
    private static DeviceSnapshot ReadPublished(CompanionService service)
    {
        var config = service.GetConfiguration(); return ObservationRules.ParseSnapshot(File.ReadAllText(Path.Combine(config.CloudFolder!, config.DeviceId + ".json")), config.GroupId!);
    }
    [Fact]
    public async Task TwoDevicesConvergeWithoutReexportAndSourceRemovalRetractsContribution()
    {
        var a = Service("A"); var b = Service("B"); Configure(a, "A", "hs-a"); Configure(b, "B", "hs-b", 220, "Player-2-CD");
        await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await b.JoinSyncFolderAsync(Path.Combine(root, "Cloud"));
        Assert.True((await a.SyncNowAsync()).Success); Assert.True((await b.SyncNowAsync()).Success); Assert.True((await a.SyncNowAsync()).Success);
        Assert.Equal(2, a.GetCharacters().Count); Assert.Equal(2, b.GetCharacters().Count);
        Assert.Equal("hs-a", Assert.Single(ReadPublished(a).Observations).SourceId); Assert.Equal("hs-b", Assert.Single(ReadPublished(b).Observations).SourceId);
        b.SaveConfiguration(b.GetConfiguration() with { Sources = [] }); await b.SyncNowAsync(); await a.SyncNowAsync();
        Assert.Empty(ReadPublished(b).Observations); Assert.Single(a.GetCharacters());
    }
    [Fact]
    public async Task ConflictingCopiesDeduplicateAndOldRevisionsNeverRollBack()
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var remote = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 5, Sample.Item("hs-b", 200, "Player-2-CD")) with { GroupId = config.GroupId! };
        var json = ObservationRules.CanonicalSnapshot(remote); File.WriteAllText(Path.Combine(config.CloudFolder!, "remote.json"), json); File.WriteAllText(Path.Combine(config.CloudFolder!, "remote (conflicted copy).json"), json);
        await a.SyncNowAsync(); Assert.Equal(2, a.GetCharacters().Count); Assert.Equal(2, a.GetDevices().Count);
        File.WriteAllText(Path.Combine(config.CloudFolder!, "remote.json"), ObservationRules.CanonicalSnapshot(remote with { Revision = 4, Observations = [] })); File.Delete(Path.Combine(config.CloudFolder!, "remote (conflicted copy).json"));
        await a.SyncNowAsync(); Assert.Equal(200, a.GetCharacters().Single(o => o.SourceId == "hs-b").Seconds);
        File.WriteAllText(Path.Combine(config.CloudFolder!, "remote.json"), ObservationRules.CanonicalSnapshot(remote with { Observations = [] }));
        var result = await a.SyncNowAsync(); Assert.Contains(result.Issues, i => i.Code == "revision_conflict"); Assert.Equal(200, a.GetCharacters().Single(o => o.SourceId == "hs-b").Seconds);
    }
    [Fact]
    public async Task HigherRevisionAndAuthoritativeLowerServerValueReplacePriorEstimate()
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var remote = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-b", 500, "Player-2-CD")) with { GroupId = config.GroupId! };
        var path = Path.Combine(config.CloudFolder!, "remote.json"); File.WriteAllText(path, ObservationRules.CanonicalSnapshot(remote)); await a.SyncNowAsync();
        remote = remote with { Revision = 2, Observations = [remote.Observations[0] with { ServerSeconds = 150, ServerAt = 1700000100, Seconds = 151, UpdatedAt = 1700000101 }] };
        File.WriteAllText(path, ObservationRules.CanonicalSnapshot(remote)); await a.SyncNowAsync(); Assert.Equal(151, a.GetCharacters().Single(o => o.SourceId == "hs-b").Seconds);
    }
    [Fact]
    public async Task PauseOfflineAndDetachKeepTheirDocumentedScopes()
    {
        var a = Service("A"); var b = Service("B"); Configure(a, "A", "hs-a"); var bSource = Configure(b, "B", "hs-b", 200, "Player-2-CD");
        await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await b.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync(); await b.SyncNowAsync(); await a.SyncNowAsync();
        a.PauseCloud(true); File.WriteAllText(bSource.SavedVariablesPath, Sample.Lua(bSource, Sample.Item("hs-b", 300, "Player-2-CD"))); await b.SyncNowAsync(); await a.SyncNowAsync(); Assert.Equal(200, a.GetCharacters().Single(o => o.SourceId == "hs-b").Seconds);
        a.PauseCloud(false); await a.SyncNowAsync(); Assert.Equal(300, a.GetCharacters().Single(o => o.SourceId == "hs-b").Seconds);
        var folder = a.GetConfiguration().CloudFolder!; Directory.Move(folder, folder + "-offline");
        Assert.Contains((await a.SyncNowAsync()).Issues, i => i.Code == "cloud_unavailable"); Assert.Equal(2, a.GetCharacters().Count);
        a.DetachSyncFolder(); await a.SyncNowAsync(); Assert.Single(a.GetCharacters()); Assert.Null(a.GetConfiguration().CloudFolder);
    }
    [Fact]
    public async Task CorruptSourceRetainsLastGoodCacheAndNeverWritesSavedVariables()
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a"); var original = File.ReadAllText(source.SavedVariablesPath);
        await a.SyncNowAsync(); Assert.Equal(original, File.ReadAllText(source.SavedVariablesPath));
        File.WriteAllText(source.SavedVariablesPath, "HourstoneDB = { version=2, characters={");
        Assert.Contains((await a.SyncNowAsync()).Issues, i => i.Code == "source_read_failed"); Assert.Single(a.GetCharacters());
    }
    [Fact]
    public async Task DeviceIdentityAndRevisionPersistAndNoChangesDoNotAdvanceRevision()
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); await a.SyncNowAsync(); var config = a.GetConfiguration(); var revision = a.GetDevices()[0].Revision;
        await a.SyncNowAsync(); Assert.Equal(revision, a.GetDevices()[0].Revision); a.Dispose(); services.Remove(a);
        var reopened = Service("A"); await reopened.SyncNowAsync(); Assert.Equal(config.DeviceId, reopened.GetConfiguration().DeviceId); Assert.Equal(revision, reopened.GetDevices()[0].Revision);
    }
    [Fact]
    public async Task DiscoverReadsOnlyHourstoneAndMigrationSourceIdIsAdopted()
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-real");
        var found = Assert.Single(a.DiscoverSources([source.WoWRoot])); Assert.Equal("hs-real", found.SourceId);
        a.SaveConfiguration(a.GetConfiguration() with { Sources = [source with { SourceId = "pending-123" }] });
        await a.SyncNowAsync(); Assert.Equal("hs-real", Assert.Single(a.GetConfiguration().Sources).SourceId);
        var output = File.ReadAllText(Path.Combine(source.DataAddonDirectory, "Data.lua")); Assert.Contains("[\"hs-real\"]", output);
    }
    [Fact]
    public async Task SourceRemovalClearsOnlyManagedRoutingAndLeavesForeignAddonAlone()
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a"); await a.SyncNowAsync();
        a.SaveConfiguration(a.GetConfiguration() with { Sources = [] }); await a.SyncNowAsync();
        Assert.DoesNotContain("hs-a", File.ReadAllText(Path.Combine(source.DataAddonDirectory, "Data.lua")));
        var b = Service("B"); var foreign = Configure(b, "B", "hs-b"); Directory.CreateDirectory(foreign.DataAddonDirectory); File.WriteAllText(Path.Combine(foreign.DataAddonDirectory, "Data.lua"), "foreign");
        Assert.Contains((await b.SyncNowAsync()).Issues, i => i.Code == "addon_write_failed"); Assert.Equal("foreign", File.ReadAllText(Path.Combine(foreign.DataAddonDirectory, "Data.lua")));
    }
    [Fact]
    public async Task ConcurrentSyncCallsSerializeWithoutLostRevisionOrSqliteErrors()
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); var tasks = Enumerable.Range(0, 4).Select(_ => a.SyncNowAsync()).ToArray();
        var results = await Task.WhenAll(tasks); Assert.All(results, result => Assert.True(result.Success)); Assert.Equal(1, a.GetDevices()[0].Revision);
    }
    [Fact]
    public async Task FailedDeselectionCleanupIsRetriedUntilSuccessful()
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a"); await a.SyncNowAsync();
        var dataPath = Path.Combine(source.DataAddonDirectory, "Data.lua");
        File.SetAttributes(dataPath, File.GetAttributes(dataPath) | FileAttributes.ReadOnly);
        try
        {
            a.SaveConfiguration(a.GetConfiguration() with { Sources = [] });
            Assert.Contains((await a.SyncNowAsync()).Issues, issue => issue.Code == "addon_write_failed");
            Assert.Contains("hs-a", File.ReadAllText(dataPath));
        }
        finally { File.SetAttributes(dataPath, File.GetAttributes(dataPath) & ~FileAttributes.ReadOnly); }
        Assert.True((await a.SyncNowAsync()).Success); Assert.DoesNotContain("hs-a", File.ReadAllText(dataPath));
    }
    [Fact]
    public async Task ForeignDeviceAtOwnFileNameIsNotOverwrittenOrImported()
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var path = Path.Combine(config.CloudFolder!, config.DeviceId + ".json");
        var foreign = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 20, Sample.Item("hs-foreign", 500, "Player-5-AA")) with { GroupId = config.GroupId! };
        var json = ObservationRules.CanonicalSnapshot(foreign); File.WriteAllText(path, json);
        Assert.Contains((await a.SyncNowAsync()).Issues, issue => issue.Code == "device_file_conflict");
        Assert.Equal(json, File.ReadAllText(path)); Assert.Single(a.GetCharacters());
    }
    [Fact]
    public async Task UnknownLegacyRegionIsNeverPromotedByInstallationMetadata()
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a");
        var lua = File.ReadAllText(source.SavedVariablesPath).Replace("region=\"eu\"", "region=\"unknown\"", StringComparison.Ordinal);
        File.WriteAllText(source.SavedVariablesPath, lua); await a.SyncNowAsync();
        Assert.Equal("unknown", Assert.Single(a.GetCharacters()).Region);
        Assert.Equal("eu", Assert.Single(a.GetConfiguration().Sources).Region);
        File.WriteAllText(source.SavedVariablesPath, lua.Replace("region=\"unknown\",", "", StringComparison.Ordinal)); await a.SyncNowAsync();
        Assert.Equal("unknown", Assert.Single(a.GetCharacters()).Region);
    }
    [Fact]
    public async Task UnknownAndCorruptRemoteSnapshotsPreservePreviouslyAcceptedRevision()
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var path = Path.Combine(config.CloudFolder!, "remote.json");
        var remote = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-remote", 500, "Player-5-AA")) with { GroupId = config.GroupId! };
        File.WriteAllText(path, ObservationRules.CanonicalSnapshot(remote)); await a.SyncNowAsync();
        File.WriteAllText(path, ObservationRules.CanonicalSnapshot(remote with { FormatVersion = 999, Revision = 2, Observations = [] }));
        Assert.Contains((await a.SyncNowAsync()).Issues, issue => issue.Code == "snapshot_rejected"); Assert.Equal(2, a.GetCharacters().Count);
        File.WriteAllText(path, "{ partial"); Assert.Contains((await a.SyncNowAsync()).Issues, issue => issue.Code == "snapshot_rejected"); Assert.Equal(2, a.GetCharacters().Count);
    }
    [Theory]
    [InlineData("_ptr_")]
    [InlineData("_xptr_")]
    [InlineData("_classic_ptr_")]
    [InlineData("_BeTa_")]
    [InlineData("_test_")]
    public void TestClientInstallationsCannotBeDiscoveredOrManuallySelected(string clientName)
    {
        var a = Service("A"); var live = Configure(a, "A", "hs-live");
        var unsupported = live with { SourceId = "hs-test-client", ClientDirectory = Path.Combine(live.WoWRoot, clientName) };
        Directory.CreateDirectory(Path.GetDirectoryName(unsupported.SavedVariablesPath)!);
        File.WriteAllText(unsupported.SavedVariablesPath, Sample.Lua(unsupported, Sample.Item(unsupported.SourceId)));
        Assert.Equal(live.ClientDirectory, Assert.Single(a.DiscoverSources([live.WoWRoot])).ClientDirectory);
        Assert.Empty(a.DiscoverSources([unsupported.ClientDirectory]));
        Assert.Throws<InvalidDataException>(() => a.SaveConfiguration(a.GetConfiguration() with { Sources = [unsupported] }));
    }
    [Fact]
    public async Task DamagedOwnedDataAddonIsRegeneratedWithoutTouchingOriginalSource()
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a"); await a.SyncNowAsync();
        var original = File.ReadAllBytes(source.SavedVariablesPath); var data = Path.Combine(source.DataAddonDirectory, "Data.lua");
        File.WriteAllBytes(data, [0xFF, 0xFE, 0xFA, 0x00]);
        Assert.True((await a.SyncNowAsync()).Success);
        Assert.StartsWith("-- Generated by Hourstone Companion", File.ReadAllText(data));
        Assert.Equal(original, File.ReadAllBytes(source.SavedVariablesPath));
    }
    [Fact]
    public async Task ResultSeparatesLocalAddonReadinessFromActualCloudPublication()
    {
        var a = Service("A"); var empty = await a.SyncNowAsync(); Assert.False(empty.AddonReady); Assert.False(empty.CloudPublished);
        var source = Configure(a, "A", "hs-a"); var local = await a.SyncNowAsync(); Assert.True(local.AddonReady); Assert.False(local.CloudPublished);
        await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); var published = await a.SyncNowAsync(); Assert.True(published.AddonReady); Assert.True(published.CloudPublished);
        a.PauseCloud(true); var paused = await a.SyncNowAsync(); Assert.True(paused.AddonReady); Assert.False(paused.CloudPublished);
        File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, Sample.Item(source.SourceId), schema: 1));
        var pending = await a.SyncNowAsync(); Assert.False(pending.AddonReady); Assert.False(pending.CloudPublished);
    }
    public void Dispose()
    {
        foreach (var service in services) service.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root); var temporary = Path.GetFullPath(Path.GetTempPath());
        if (full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.Companion.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
