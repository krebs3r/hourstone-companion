using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class ClientHintsTests : IDisposable
{
    private const string Group = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", Device = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone.ClientHints.Tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<CompanionService> services = [];
    private string Cloud => Path.Combine(root, "Cloud");
    private CompanionService Service(string name)
    {
        var service = new CompanionService(Path.Combine(root, name, "state.db"), name); services.Add(service); return service;
    }
    private SourceConfiguration Configure(CompanionService service, string name, string client, string guid, string? sourceId = null)
    {
        var wow = Path.Combine(root, name, "WoW");
        var source = Sample.Source(sourceId ?? "hs-" + name) with { WoWRoot = wow, ClientDirectory = Path.Combine(wow, client) };
        Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!); File.WriteAllText(source.AddonTocPath, "## Version: 0.3.4\n");
        Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!);
        File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, Sample.Item(source.SourceId, guid: guid), 3));
        service.SaveConfiguration(service.GetConfiguration() with { Sources = [source] }); return source;
    }
    private static async Task SyncAll(params CompanionService[] devices)
    {
        for (var round = 0; round < 2; round++) foreach (var device in devices) Assert.True((await device.SyncNowAsync()).Success);
    }
    private static string Identity(CompanionService service, string guid) => ObservationRules.Identity(service.GetCharacters().Single(c => c.Guid == guid));

    [Fact]
    public async Task PeersLabelForeverCharactersWithoutChangingTheSnapshot()
    {
        var forever = Service("A"); var retail = Service("B");
        Configure(forever, "A", "_forever_", "Player-1-AA"); Configure(retail, "B", "_retail_", "Player-1-BB");
        foreach (var device in new[] { forever, retail }) await device.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, retail);
        Assert.Equal([Identity(retail, "Player-1-AA")], retail.GetForeverIdentities());
        Assert.Equal([Identity(forever, "Player-1-AA")], forever.GetForeverIdentities());
        var foreverId = forever.GetConfiguration().DeviceId; var folder = forever.GetConfiguration().CloudFolder!;
        // Older Companions enumerate only *.json, so the hint is invisible to them; the snapshot stays format 4 and Retail.
        Assert.Equal([foreverId + ClientHints.Extension], Directory.GetFiles(folder, "*" + ClientHints.Extension).Select(Path.GetFileName));
        Assert.DoesNotContain(Directory.GetFiles(folder, "*.json"), path => path.EndsWith(ClientHints.Extension, StringComparison.Ordinal));
        var snapshot = File.ReadAllText(Path.Combine(folder, foreverId + ".json"));
        Assert.Equal(4, ObservationRules.ParseSnapshot(snapshot, forever.GetConfiguration().GroupId!).FormatVersion);
        Assert.DoesNotContain("forever", snapshot, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task HintsAreDisplayOnlyFailSoftAndWithdrawn()
    {
        var forever = Service("A"); var retail = Service("B");
        var source = Configure(forever, "A", "_forever_", "Player-1-AA"); Configure(retail, "B", "_retail_", "Player-1-BB");
        foreach (var device in new[] { forever, retail }) await device.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, retail);
        var hint = Path.Combine(forever.GetConfiguration().CloudFolder!, forever.GetConfiguration().DeviceId + ClientHints.Extension);
        Assert.True(File.Exists(hint));
        File.WriteAllText(hint, "{ not json");
        Assert.True((await retail.SyncNowAsync()).Success); Assert.Single(retail.GetForeverIdentities());
        // A hint that cannot be withdrawn is retried later and does not turn into a sync-folder issue.
        File.SetAttributes(hint, FileAttributes.ReadOnly);
        forever.SaveConfiguration(forever.GetConfiguration() with { Sources = [source with { Enabled = false }] });
        try { Assert.True((await forever.SyncNowAsync()).Success); Assert.True(File.Exists(hint)); }
        finally { File.SetAttributes(hint, FileAttributes.Normal); }
        await SyncAll(forever, retail);
        Assert.False(File.Exists(hint)); Assert.Empty(retail.GetForeverIdentities());
    }
    [Fact]
    public async Task DisconnectingForgetsPeerHints()
    {
        var forever = Service("A"); var retail = Service("B");
        Configure(forever, "A", "_forever_", "Player-1-AA"); Configure(retail, "B", "_retail_", "Player-1-BB");
        foreach (var device in new[] { forever, retail }) await device.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, retail); Assert.Single(retail.GetForeverIdentities());
        retail.DetachSyncFolder(); Assert.Empty(retail.GetForeverIdentities());
        // Rejoining with an unreadable hint must not revive the hint received before disconnecting.
        File.WriteAllText(Path.Combine(forever.GetConfiguration().CloudFolder!, forever.GetConfiguration().DeviceId + ClientHints.Extension), "{ not json");
        await retail.JoinSyncFolderAsync(Cloud); Assert.True((await retail.SyncNowAsync()).Success);
        Assert.Contains(retail.GetCharacters(), c => c.Guid == "Player-1-AA"); Assert.Empty(retail.GetForeverIdentities());
    }
    [Fact]
    public async Task APeerHintNeverLabelsAnotherClientsUseOfTheSameSource()
    {
        var forever = Service("A"); var retail = Service("B"); var other = Service("C");
        Configure(forever, "A", "_forever_", "Player-1-AA"); Configure(retail, "B", "_retail_", "Player-1-BB");
        await forever.JoinSyncFolderAsync(Cloud); await retail.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, retail);
        Assert.Equal([Identity(retail, "Player-1-AA")], retail.GetForeverIdentities());
        // C's Retail installation uses A's source ID: a copied WTF folder, so the Forever copy yields, as in SourceDiscovery.AddDiscovered.
        Configure(other, "C", "_retail_", "Player-1-CC", "hs-A"); await other.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, retail, other);
        Assert.Contains(retail.GetCharacters(), c => c.Guid == "Player-1-CC"); Assert.Empty(retail.GetForeverIdentities());
    }
    [Fact]
    public async Task DuplicateStoredHintsNeverStopTheServiceFromStarting()
    {
        var forever = Service("A"); var retail = Service("B");
        Configure(forever, "A", "_forever_", "Player-1-AA"); Configure(retail, "B", "_retail_", "Player-1-BB");
        foreach (var device in new[] { forever, retail }) await device.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, retail);
        var config = retail.GetConfiguration(); var hint = new ClientHints { GroupId = config.GroupId!, DeviceId = forever.GetConfiguration().DeviceId, ForeverSources = ["hs-A"] };
        retail.Dispose(); var database = Path.Combine(root, "B", "state.db");
        using (var store = new CompanionStore(database))
            store.Set("clientHints", System.Text.Json.JsonSerializer.Serialize(new[] { hint, hint with { DeviceId = hint.DeviceId.ToUpperInvariant() } }, JsonContract.Options));
        Assert.Single(Service("B").GetForeverIdentities());
    }
    [Fact]
    public void ParsingRejectsForeignMalformedAndFutureHints()
    {
        var valid = ClientHints.Canonical(new ClientHints { GroupId = Group, DeviceId = Device, ForeverSources = ["hs-b", "hs-a", "hs-a"] });
        Assert.Equal(["hs-a", "hs-b"], ClientHints.Parse(valid, Group, Device).ForeverSources);
        Assert.Equal(valid, ClientHints.Canonical(ClientHints.Parse(valid, Group, Device)));
        Assert.Throws<InvalidDataException>(() => ClientHints.Parse(valid, Guid.NewGuid().ToString("D"), Device));
        Assert.Throws<InvalidDataException>(() => ClientHints.Parse(valid, Group, Guid.NewGuid().ToString("D")));
        Assert.Throws<InvalidDataException>(() => ClientHints.Parse(valid.Replace("\"hs-b\"", "\"hs-a\"", StringComparison.Ordinal), Group, Device));
        Assert.Throws<InvalidDataException>(() => ClientHints.Parse(valid.Replace("\"hs-b\"", "\"bad id\"", StringComparison.Ordinal), Group, Device));
        Assert.Throws<InvalidDataException>(() => ClientHints.Parse(valid.Replace("\"formatVersion\": 1", "\"formatVersion\": 2", StringComparison.Ordinal), Group, Device));
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ClientHints.Parse(valid.Replace("}", ",\"extra\":1}", StringComparison.Ordinal), Group, Device));
    }
    public void Dispose()
    {
        foreach (var service in services) service.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.ClientHints.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
