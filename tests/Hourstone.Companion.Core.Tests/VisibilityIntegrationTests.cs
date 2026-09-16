using System.Globalization;
using System.Text.Json;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class VisibilityIntegrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone.Visibility.Tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<CompanionService> services = [];
    private CompanionService Service(string name)
    {
        var service = new CompanionService(Path.Combine(root, name, "state.db"), name); services.Add(service); return service;
    }
    private SourceConfiguration Configure(CompanionService service, string name, string guid = "Player-1-AB")
    {
        var wow = Path.Combine(root, name, "WoW");
        var source = Sample.Source("hs-" + name) with { WoWRoot = wow, ClientDirectory = Path.Combine(wow, "_retail_") };
        Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!); File.WriteAllText(source.AddonTocPath, "## Version: 0.2.2\n");
        WriteSaved(source, Sample.Item(source.SourceId, guid: guid));
        service.SaveConfiguration(service.GetConfiguration() with { Sources = [source] }); return source;
    }
    private static void WriteSaved(SourceConfiguration source, Observation observation, params CharacterVisibility[] visibility)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!);
        var literal = "visibility={" + string.Join(',', visibility.Select(StateLua)) + "}";
        File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, observation, 3).Replace("visibility={}", literal, StringComparison.Ordinal));
    }
    private static string StateLua(CharacterVisibility state) =>
        "{sourceId=" + DataAddonWriter.Quote(state.SourceId) + ",region=" + DataAddonWriter.Quote(state.Region) + ",flavor=" + DataAddonWriter.Quote(state.Flavor) + ",guid=" + DataAddonWriter.Quote(state.Guid) +
        ",removed=" + MapLua(state.Removed) + ",restored=" + MapLua(state.Restored) + "}";
    private static string MapLua(Dictionary<string, long> map) => "{" + string.Join(',', map.Select(pair => "[" + DataAddonWriter.Quote(pair.Key) + "]=" + pair.Value.ToString(CultureInfo.InvariantCulture))) + "}";
    private static DeviceSnapshot Published(CompanionService service)
    {
        var config = service.GetConfiguration(); return ObservationRules.ParseSnapshot(File.ReadAllText(Path.Combine(config.CloudFolder!, config.DeviceId + ".json")), config.GroupId!);
    }
    private async Task Connect(params CompanionService[] devices)
    {
        foreach (var device in devices) await device.JoinSyncFolderAsync(Path.Combine(root, "Cloud"));
        foreach (var device in devices) Assert.True((await device.SyncNowAsync()).Success);
        foreach (var device in devices) Assert.True((await device.SyncNowAsync()).Success);
    }

    [Fact]
    public async Task RemoveAndRestoreConvergeWithoutDroppingOrReexportingForeignPlaytime()
    {
        var a = Service("A"); var b = Service("B"); var sourceA = Configure(a, "A"); var sourceB = Configure(b, "B");
        var originalA = File.ReadAllBytes(sourceA.SavedVariablesPath); var originalB = File.ReadAllBytes(sourceB.SavedVariablesPath);
        await Connect(a, b); a.SetCharacterRemoved(Assert.Single(a.GetCharacters()), true);
        Assert.Empty(a.GetCharacters()); Assert.Single(a.GetRemovedCharacters());
        Assert.True((await a.SyncNowAsync()).Success); Assert.True((await b.SyncNowAsync()).Success);
        Assert.Empty(b.GetCharacters()); Assert.Single(b.GetRemovedCharacters());
        Assert.Equal(3, Published(b).FormatVersion); Assert.Single(Published(b).Visibility!);
        Assert.Equal(sourceA.SourceId, Assert.Single(Published(a).Observations).SourceId);
        Assert.Equal(sourceB.SourceId, Assert.Single(Published(b).Observations).SourceId);
        var generated = File.ReadAllText(Path.Combine(sourceB.DataAddonDirectory, "Data.lua"));
        Assert.Contains("seconds=120", generated); Assert.Contains("visibility = {", generated);
        Assert.Contains("## Version: 3.0.0", File.ReadAllText(Path.Combine(sourceB.DataAddonDirectory, "Hourstone_Sync.toc")));
        b.SetCharacterRemoved(Assert.Single(b.GetRemovedCharacters()), false);
        Assert.True((await b.SyncNowAsync()).Success); Assert.True((await a.SyncNowAsync()).Success);
        Assert.Single(a.GetCharacters()); Assert.Empty(a.GetRemovedCharacters());
        Assert.Equal(originalA, File.ReadAllBytes(sourceA.SavedVariablesPath)); Assert.Equal(originalB, File.ReadAllBytes(sourceB.SavedVariablesPath));
    }

    [Fact]
    public async Task ConcurrentUnseenRemoveSurvivesRestoreUntilExplicitlyAcknowledged()
    {
        var a = Service("A"); var b = Service("B"); Configure(a, "A"); Configure(b, "B"); await Connect(a, b);
        a.SetCharacterRemoved(Assert.Single(a.GetCharacters()), true); await a.SyncNowAsync(); await b.SyncNowAsync();
        b.PauseCloud(true); b.SetCharacterRemoved(Assert.Single(b.GetRemovedCharacters()), true);
        a.SetCharacterRemoved(Assert.Single(a.GetRemovedCharacters()), false); await a.SyncNowAsync();
        Assert.Single(a.GetCharacters()); b.PauseCloud(false); await b.SyncNowAsync(); await a.SyncNowAsync();
        Assert.Empty(a.GetCharacters()); Assert.Empty(b.GetCharacters());
        var pending = Assert.Single(Published(a).Visibility!); Assert.Equal(2, pending.Removed.Count);
        Assert.Single(pending.Restored); Assert.True(VisibilityRules.IsRemoved(pending));
        a.SetCharacterRemoved(Assert.Single(a.GetRemovedCharacters()), false); await a.SyncNowAsync(); await b.SyncNowAsync();
        Assert.Single(b.GetCharacters()); Assert.False(VisibilityRules.IsRemoved(Assert.Single(Published(b).Visibility!)));
    }

    [Fact]
    public async Task OldSavedStatesStalePlaytimeAndRestartCannotResurrectRemovedCharacter()
    {
        var service = Service("A"); var source = Configure(service, "A"); await Connect(service);
        service.SetCharacterRemoved(Assert.Single(service.GetCharacters()), true); await service.SyncNowAsync();
        var removed = Assert.Single(Published(service).Visibility!);
        WriteSaved(source, Sample.Item(source.SourceId, 900), removed);
        await service.SyncNowAsync(); Assert.Empty(service.GetCharacters()); Assert.Equal(900, Assert.Single(service.GetRemovedCharacters()).Seconds);
        service.Dispose(); services.Remove(service); service = Service("A");
        Assert.Empty(service.GetCharacters()); Assert.Single(service.GetRemovedCharacters());
        service.SetCharacterRemoved(Assert.Single(service.GetRemovedCharacters()), false); await service.SyncNowAsync();
        Assert.Single(service.GetCharacters()); Assert.Single(Published(service).Visibility!.Single().Restored);
        // WoW writes the old remove again. The retained acknowledgment still wins.
        await service.SyncNowAsync(); Assert.Single(service.GetCharacters());
        service.SetCharacterRemoved(Assert.Single(service.GetCharacters()), true); await service.SyncNowAsync();
        Assert.Empty(service.GetCharacters());
        var freshRuntimeRemove = Assert.Single(Published(service).Visibility!);
        Assert.Equal(2, freshRuntimeRemove.Removed.Count); Assert.Single(freshRuntimeRemove.Restored);
    }

    [Fact]
    public async Task ReadOnlyAndCorruptSavedVariablesRetainLastValidControlAndObservation()
    {
        var service = Service("A"); var source = Configure(service, "A"); await Connect(service);
        var original = File.ReadAllBytes(source.SavedVariablesPath);
        File.SetAttributes(source.SavedVariablesPath, File.GetAttributes(source.SavedVariablesPath) | FileAttributes.ReadOnly);
        try
        {
            service.SetCharacterRemoved(Assert.Single(service.GetCharacters()), true);
            Assert.True((await service.SyncNowAsync()).Success); Assert.Equal(original, File.ReadAllBytes(source.SavedVariablesPath));
        }
        finally { File.SetAttributes(source.SavedVariablesPath, File.GetAttributes(source.SavedVariablesPath) & ~FileAttributes.ReadOnly); }
        var malformed = VisibilityRules.FromObservation(Sample.Item(source.SourceId)) with { Removed = new() { ["bad"] = 1 }, Restored = new() { ["bad"] = 2 } };
        WriteSaved(source, Sample.Item(source.SourceId, 999), malformed);
        Assert.Contains((await service.SyncNowAsync()).Issues, issue => issue.Code == "source_read_failed");
        Assert.Equal(120, Assert.Single(service.GetRemovedCharacters()).Seconds); Assert.Empty(service.GetCharacters());
        Assert.Single(Assert.Single(Published(service).Visibility!).Removed);
    }

    [Fact]
    public async Task SelectionPauseAndDetachPreserveHistoryButOnlyCurrentIdentitiesArePublished()
    {
        var a = Service("A"); var b = Service("B"); var sourceA = Configure(a, "A", "Player-LOCAL"); Configure(b, "B", "Player-OLDGROUP"); await Connect(a, b);
        a.SetCharacterRemoved(a.GetCharacters().Single(item => item.Guid == "Player-OLDGROUP"), true); await a.SyncNowAsync();
        Assert.Single(Published(a).Visibility!);
        await a.JoinSyncFolderAsync(Path.Combine(root, "NewGroup")); await a.SyncNowAsync();
        Assert.Empty(Published(a).Visibility!); Assert.Single(a.GetCharacters()); Assert.Empty(a.GetRemovedCharacters());
        Assert.DoesNotContain("Player-OLDGROUP", File.ReadAllText(Path.Combine(sourceA.DataAddonDirectory, "Data.lua")));
        await a.JoinSyncFolderAsync(Path.Combine(root, "Cloud")); await a.SyncNowAsync();
        Assert.Equal("Player-OLDGROUP", Assert.Single(a.GetRemovedCharacters()).Guid);
        a.SetCharacterRemoved(Assert.Single(a.GetCharacters()), true); a.PauseCloud(true);
        Assert.False((await a.SyncNowAsync()).CloudPublished); Assert.Empty(a.GetCharacters());
        a.SaveConfiguration(a.GetConfiguration() with { Sources = [] }); a.DetachSyncFolder(); await a.SyncNowAsync();
        Assert.Empty(a.GetCharacters()); Assert.Empty(a.GetRemovedCharacters());
        a.SaveConfiguration(a.GetConfiguration() with { Sources = [sourceA] }); await a.SyncNowAsync();
        Assert.Empty(a.GetCharacters()); Assert.Equal("Player-LOCAL", Assert.Single(a.GetRemovedCharacters()).Guid);
    }

    [Fact]
    public async Task ExcessivePeerDoesNotPoisonCacheOrBlockUnrelatedValidPeer()
    {
        var service = Service("A"); var source = Configure(service, "A");
        var full = VisibilityRules.FromObservation(Sample.Item(source.SourceId)) with { Removed = Enumerable.Range(0, VisibilityRules.MaximumActors).ToDictionary(n => "actor-" + n, _ => 1L) };
        WriteSaved(source, Sample.Item(source.SourceId), full); await Connect(service);
        var config = service.GetConfiguration();
        var bad = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-bad", 999)) with
        { GroupId = config.GroupId!, FormatVersion = 3, Visibility = [VisibilityRules.Remove(VisibilityRules.FromObservation(Sample.Item("hs-bad")), "extra-actor")] };
        var good = Sample.Snapshot("22222222-2222-2222-2222-222222222222", 1, Sample.Item("hs-good", 220, "Player-GOOD")) with { GroupId = config.GroupId!, FormatVersion = 3, Visibility = [] };
        File.WriteAllText(Path.Combine(config.CloudFolder!, "bad.json"), ObservationRules.CanonicalSnapshot(bad));
        File.WriteAllText(Path.Combine(config.CloudFolder!, "good.json"), ObservationRules.CanonicalSnapshot(good));
        var result = await service.SyncNowAsync(); Assert.Contains(result.Issues, issue => issue.Code == "snapshot_rejected");
        Assert.Equal(120, Assert.Single(service.GetRemovedCharacters()).Seconds); Assert.Equal("Player-GOOD", Assert.Single(service.GetCharacters()).Guid);
        Assert.Equal(2, service.GetDevices().Count); Assert.DoesNotContain(service.GetDevices(), device => device.DeviceId == bad.DeviceId);
        Assert.Equal(VisibilityRules.MaximumActors, Assert.Single(Published(service).Visibility!).Removed.Count);
    }

    [Fact]
    public async Task SourceCumulativeLimitFailureDoesNotPartiallyReplaceLastGoodSource()
    {
        var service = Service("A"); var source = Configure(service, "A"); await Connect(service);
        service.SetCharacterRemoved(Assert.Single(service.GetCharacters()), true); await service.SyncNowAsync();
        var excessive = VisibilityRules.FromObservation(Sample.Item(source.SourceId)) with { Removed = Enumerable.Range(0, VisibilityRules.MaximumActors).ToDictionary(n => "actor-" + n, _ => 1L) };
        WriteSaved(source, Sample.Item(source.SourceId, 999), excessive);
        Assert.Contains((await service.SyncNowAsync()).Issues, issue => issue.Code == "source_read_failed");
        Assert.Equal(120, Assert.Single(service.GetRemovedCharacters()).Seconds);
        Assert.Single(Assert.Single(Published(service).Visibility!).Removed);
    }

    [Fact]
    public async Task UnknownCharacterMutationIsRejectedWithoutIntroducingControlState()
    {
        var service = Service("A"); Configure(service, "A"); await Connect(service);
        Assert.Throws<InvalidDataException>(() => service.SetCharacterRemoved(Sample.Item("hs-unknown", guid: "Player-UNKNOWN"), true));
        await service.SyncNowAsync(); Assert.Empty(Published(service).Visibility!); Assert.Single(service.GetCharacters());
    }

    public void Dispose()
    {
        foreach (var service in services) service.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.Visibility.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
