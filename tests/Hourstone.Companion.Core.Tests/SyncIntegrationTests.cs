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
        Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!); File.WriteAllText(source.AddonTocPath, "## Version: 0.2.2\n");
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
        Assert.Contains((await a.SyncNowAsync()).Issues, issue => issue.Code == "snapshot_rejected" && issue.FilePath == path); Assert.Equal(2, a.GetCharacters().Count);
        File.WriteAllText(path, "{ partial"); Assert.Contains((await a.SyncNowAsync()).Issues, issue => issue.Code == "snapshot_rejected" && issue.FilePath == path); Assert.Equal(2, a.GetCharacters().Count);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailablePeerKeepsItsCacheWithoutBlockingHealthyPeersAndRecovers(bool offline)
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var path = Path.Combine(config.CloudFolder!, "peer.json");
        var peer = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-peer", 200, "Player-2-AA")) with { GroupId = config.GroupId! };
        File.WriteAllText(path, ObservationRules.CanonicalSnapshot(peer)); await a.SyncNowAsync();
        peer = peer with { Revision = 2, Observations = [peer.Observations[0] with { Seconds = 500 }] };
        File.WriteAllText(path, ObservationRules.CanonicalSnapshot(peer));
        var healthy = Sample.Snapshot("22222222-2222-2222-2222-222222222222", 1, Sample.Item("hs-healthy", 700, "Player-3-AA")) with { GroupId = config.GroupId! };
        File.WriteAllText(Path.Combine(config.CloudFolder!, "healthy.json"), ObservationRules.CanonicalSnapshot(healthy));
        var attributes = File.GetAttributes(path); FileStream? held = null;
        try
        {
            if (offline) File.SetAttributes(path, attributes | FileAttributes.Offline);
            else held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var blocked = await a.SyncNowAsync(); var issue = Assert.Single(blocked.Issues);
            Assert.Equal(offline ? "cloud_file_not_local" : "snapshot_read_failed", issue.Code); Assert.Equal(path, issue.FilePath);
            Assert.True(blocked.CloudPublished); Assert.Equal(3, a.GetCharacters().Count);
            Assert.Equal(200, a.GetCharacters().Single(item => item.SourceId == "hs-peer").Seconds);
            Assert.Equal(700, a.GetCharacters().Single(item => item.SourceId == "hs-healthy").Seconds);
        }
        finally { held?.Dispose(); if (offline) File.SetAttributes(path, attributes); }
        var recovered = await a.SyncNowAsync(); Assert.True(recovered.Success); Assert.True(recovered.CloudPublished);
        Assert.Equal(500, a.GetCharacters().Single(item => item.SourceId == "hs-peer").Seconds);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnreadableOwnSnapshotIsNotOverwrittenAndPublicationRecovers(bool offline)
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var path = Path.Combine(config.CloudFolder!, config.DeviceId + ".json"); var original = File.ReadAllText(path);
        File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, Sample.Item(source.SourceId, 300)));
        var attributes = File.GetAttributes(path); FileStream? held = null;
        try
        {
            if (offline) File.SetAttributes(path, attributes | FileAttributes.Offline);
            else held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var blocked = await a.SyncNowAsync(); var issue = Assert.Single(blocked.Issues);
            Assert.Equal(offline ? "cloud_file_not_local" : "snapshot_read_failed", issue.Code); Assert.Equal(path, issue.FilePath);
            Assert.False(blocked.CloudPublished); Assert.Equal(300, Assert.Single(a.GetCharacters()).Seconds);
        }
        finally { held?.Dispose(); if (offline) File.SetAttributes(path, attributes); }
        Assert.Equal(original, File.ReadAllText(path));
        var recovered = await a.SyncNowAsync(); Assert.True(recovered.Success); Assert.True(recovered.CloudPublished);
        Assert.Equal(300, Assert.Single(ReadPublished(a).Observations).Seconds);
    }
    [Theory]
    [InlineData("group.json", false)]
    [InlineData("group.json", true)]
    [InlineData("group (conflict).json", false)]
    [InlineData("group (conflict).json", true)]
    public async Task UnavailableGroupFileReportsItsPathAndBlocksPublicationUntilRecovery(string fileName, bool offline)
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var path = Path.Combine(config.CloudFolder!, fileName);
        if (fileName != "group.json") File.Copy(Path.Combine(config.CloudFolder!, "group.json"), path);
        var ownPath = Path.Combine(config.CloudFolder!, config.DeviceId + ".json"); var original = File.ReadAllText(ownPath);
        File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, Sample.Item(source.SourceId, 300)));
        var attributes = File.GetAttributes(path); FileStream? held = null;
        try
        {
            if (offline) File.SetAttributes(path, attributes | FileAttributes.Offline);
            else held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var blocked = await a.SyncNowAsync(); var issue = Assert.Single(blocked.Issues);
            Assert.Equal(offline ? "cloud_file_not_local" : "snapshot_read_failed", issue.Code); Assert.Equal(path, issue.FilePath);
            Assert.False(blocked.CloudPublished); Assert.Equal(original, File.ReadAllText(ownPath));
        }
        finally { held?.Dispose(); if (offline) File.SetAttributes(path, attributes); }
        var recovered = await a.SyncNowAsync(); Assert.True(recovered.Success); Assert.True(recovered.CloudPublished);
        Assert.Equal(300, Assert.Single(ReadPublished(a).Observations).Seconds);
    }
    [Fact]
    public async Task AggregateSnapshotLimitStillBlocksPublication()
    {
        var a = Service("A"); Configure(a, "A", "hs-a"); await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration(); var ownPath = Path.Combine(config.CloudFolder!, config.DeviceId + ".json"); var original = File.ReadAllText(ownPath);
        foreach (var name in new[] { "large-a.json", "large-b.json" })
            using (var file = new FileStream(Path.Combine(config.CloudFolder!, name), FileMode.CreateNew)) file.SetLength(33L * 1024 * 1024);
        var blocked = await a.SyncNowAsync(); Assert.False(blocked.CloudPublished);
        Assert.Contains(blocked.Issues, issue => issue.Code == "cloud_unavailable" && issue.Message.Contains("total snapshot size limit", StringComparison.Ordinal));
        Assert.Equal(original, File.ReadAllText(ownPath)); Assert.Single(a.GetCharacters());
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
    [Fact]
    public async Task GuildChangesConvergeWithoutReplacingPlayedBaselineOrReexportingForeignGuilds()
    {
        var a = Service("A"); var b = Service("B");
        var sourceA = Configure(a, "A", "hs-a"); var sourceB = Configure(b, "B", "hs-b");
        var local = Sample.Item("hs-a", 220) with { ServerSeconds = 200, ServerAt = 1700000100, UpdatedAt = 1700000120, Guild = "Old guild", GuildUpdatedAt = 1700000100 };
        var remote = Sample.Item("hs-b") with { Guild = "Neue Gilde", GuildUpdatedAt = 1700000300 };
        File.WriteAllText(sourceA.SavedVariablesPath, Sample.Lua(sourceA, local));
        File.WriteAllText(sourceB.SavedVariablesPath, Sample.Lua(sourceB, remote));
        var originalA = File.ReadAllText(sourceA.SavedVariablesPath);
        await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await b.JoinSyncFolderAsync(Path.Combine(root, "Cloud"));
        await b.SyncNowAsync(); await a.SyncNowAsync();
        var row = Assert.Single(a.GetCharacters()); Assert.Equal(220, row.Seconds); Assert.Equal(local.ServerAt, row.ServerAt); Assert.Equal("Neue Gilde", row.Guild);
        Assert.Equal("Old guild", Assert.Single(ReadPublished(a).Observations).Guild);
        Assert.Equal("Neue Gilde", Assert.Single(ReadPublished(b).Observations).Guild); Assert.Equal(4, ReadPublished(a).FormatVersion);
        Assert.Contains("guild=\"Neue Gilde\"", File.ReadAllText(Path.Combine(sourceA.DataAddonDirectory, "Data.lua")));
        remote = remote with { Guild = "", GuildUpdatedAt = 1700000400 };
        File.WriteAllText(sourceB.SavedVariablesPath, Sample.Lua(sourceB, remote));
        await b.SyncNowAsync(); await a.SyncNowAsync(); Assert.Equal("", Assert.Single(a.GetCharacters()).Guild);
        Assert.Equal(220, Assert.Single(a.GetCharacters()).Seconds); Assert.Equal(originalA, File.ReadAllText(sourceA.SavedVariablesPath));
        File.WriteAllText(sourceB.SavedVariablesPath, "corrupted data");
        Assert.Contains((await b.SyncNowAsync()).Issues, issue => issue.Code == "source_read_failed");
        await a.SyncNowAsync(); Assert.Equal("", Assert.Single(a.GetCharacters()).Guild);
        a.Dispose(); services.Remove(a); a = Service("A"); await a.SyncNowAsync();
        Assert.Equal("", Assert.Single(a.GetCharacters()).Guild); Assert.Equal("Old guild", Assert.Single(ReadPublished(a).Observations).Guild);
        b.SaveConfiguration(b.GetConfiguration() with { Sources = [] }); await b.SyncNowAsync(); await a.SyncNowAsync();
        Assert.Equal("Old guild", Assert.Single(a.GetCharacters()).Guild); Assert.Equal(220, Assert.Single(a.GetCharacters()).Seconds);
    }
    [Fact]
    public async Task LegacyCachedSnapshotsUpgradeOwnRevisionAndRetainUnchangedPeerWithoutFalseConflict()
    {
        var a = Service("A"); var source = Configure(a, "A", "hs-a");
        await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        var config = a.GetConfiguration();
        var remote = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 5, Sample.Item("hs-peer", 500, "Player-2-CD")) with { FormatVersion = 1, GroupId = config.GroupId! };
        var path = Path.Combine(config.CloudFolder!, "peer.json"); File.WriteAllText(path, ObservationRules.CanonicalSnapshot(remote));
        await a.SyncNowAsync(); var legacyOwn = ReadPublished(a) with { FormatVersion = 1, Visibility = null, ProgressObservations = null };
        var legacyJson = ObservationRules.CanonicalSnapshot(legacyOwn); Assert.DoesNotContain("guild", legacyJson);
        a.Dispose(); services.Remove(a);
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + Path.Combine(root, "A", "app", "state.db")))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "UPDATE snapshots SET json=$json,hash=$hash WHERE device_id=$device";
            command.Parameters.AddWithValue("$json", legacyJson); command.Parameters.AddWithValue("$hash", SafeFiles.Sha256(legacyJson)); command.Parameters.AddWithValue("$device", config.DeviceId); command.ExecuteNonQuery();
        }
        File.WriteAllText(Path.Combine(config.CloudFolder!, config.DeviceId + ".json"), legacyJson);
        a = Service("A"); var result = await a.SyncNowAsync(); Assert.True(result.Success, string.Join("; ", result.Issues));
        var upgraded = ReadPublished(a); Assert.Equal(4, upgraded.FormatVersion); Assert.Equal(legacyOwn.Revision + 1, upgraded.Revision); Assert.Equal(config.DeviceId, upgraded.DeviceId);
        Assert.Equal(source.SourceId, Assert.Single(a.GetConfiguration().Sources).SourceId); Assert.Equal(2, a.GetCharacters().Count);
        Assert.Null(a.GetCharacters().Single(o => o.SourceId == "hs-peer").Guild);
        Assert.True((await a.SyncNowAsync()).Success); Assert.Equal(upgraded.Revision, ReadPublished(a).Revision);
        remote = remote with { FormatVersion = 2, Revision = 6, Observations = [remote.Observations[0] with { Guild = "Dawnwatch", GuildUpdatedAt = 1700001000 }] };
        File.WriteAllText(path, ObservationRules.CanonicalSnapshot(remote)); Assert.True((await a.SyncNowAsync()).Success);
        Assert.Equal("Dawnwatch", a.GetCharacters().Single(o => o.SourceId == "hs-peer").Guild);
    }
    public void Dispose()
    {
        foreach (var service in services) service.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root); var temporary = Path.GetFullPath(Path.GetTempPath());
        if (full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.Companion.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
