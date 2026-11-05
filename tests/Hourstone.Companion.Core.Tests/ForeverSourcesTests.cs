using System.Text.Json;
using System.Text.RegularExpressions;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

/// <summary>Protocol 4 addendum: the generated data names the WoW: Forever sources (docs/sync-protocol-v4-forever.md).</summary>
public sealed partial class ForeverSourcesTests
{
    private static JsonElement Fixture()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "forever-v4.json")));
        return document.RootElement.Clone();
    }
    private static List<string> Strings(JsonElement value) => [.. value.EnumerateArray().Select(item => item.GetString()!)];
    /// <summary>The foreverSources list of each source scope in generated data.</summary>
    internal static List<List<string>> Lists(string data) =>
        [.. ListPattern().Matches(data).Select(match => match.Groups[1].Value.Split(", ").Select(item => item.Trim('"')).ToList())];
    [GeneratedRegex("foreverSources = \\{ (.*?) \\}")]
    private static partial Regex ListPattern();

    [Fact]
    public void SharedCasesWriteTheCanonicalListIntoEveryFormatFourScope()
    {
        var fixture = Fixture();
        Assert.Equal(4, fixture.GetProperty("formatVersion").GetInt32()); Assert.Equal("foreverSources", fixture.GetProperty("field").GetString());
        foreach (var test in fixture.GetProperty("cases").EnumerateArray())
        {
            var name = test.GetProperty("name").GetString();
            var observations = JsonSerializer.Deserialize<List<Observation>>(test.GetProperty("observations").GetRawText(), JsonContract.Options)!;
            var input = Strings(test.GetProperty("foreverSources")); var expected = Strings(test.GetProperty("expectedList"));
            string[] scopes = [test.GetProperty("localSourceId").GetString()!, "hs-second-scope"];
            var data = DataAddonWriter.BuildData(scopes, observations, [], [], 4, input);
            Assert.True(expected.Count == 0 ? !data.Contains("foreverSources", StringComparison.Ordinal) : Lists(data).Count == scopes.Length, name);
            Assert.All(Lists(data), list => Assert.Equal(expected, list));
            Assert.Equal(data, DataAddonWriter.BuildData(scopes, observations, [], [], 4, [.. input.AsEnumerable().Reverse()]));
            // Formats 1 to 3 have no list.
            Assert.DoesNotContain("foreverSources", DataAddonWriter.BuildData(scopes, observations, [], [], 3, input), StringComparison.Ordinal);
        }
    }
    [Fact]
    public void GeneratedDataMatchesTheSharedGoldenFileThatTheAddonReads()
    {
        var test = Fixture().GetProperty("cases")[0];
        var observations = JsonSerializer.Deserialize<List<Observation>>(test.GetProperty("observations").GetRawText(), JsonContract.Options)!;
        var data = DataAddonWriter.BuildData([test.GetProperty("localSourceId").GetString()!], observations, [], [], 4, Strings(test.GetProperty("foreverSources")));
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "forever-data-v4.lua")), data);
    }
    [Fact]
    public void SharedInvalidSourceIdsAreRejected()
    {
        foreach (var id in Strings(Fixture().GetProperty("invalidSourceIds")))
            foreach (var format in new[] { 3, 4 })
                Assert.Throws<InvalidDataException>(() => DataAddonWriter.BuildData(["hs-local"], [Sample.Item("hs-local")], [], [], format, [id]));
    }
    [Fact]
    public void TheListFollowsProgressAndLeavesDataWithoutForeverUnchanged()
    {
        Observation[] union = [Sample.Item("hs-forever", guid: "Player-1-FF"), Sample.Item("hs-retail", guid: "Player-1-RR")];
        var plain = DataAddonWriter.BuildData(["hs-retail"], union, [], [], 4);
        Assert.Equal(plain, DataAddonWriter.BuildData(["hs-retail"], union, [], [], 4, []));
        Assert.Equal(plain, DataAddonWriter.BuildData(["hs-retail"], union, [], [], 4, ["hs-idle"]));
        var labelled = DataAddonWriter.BuildData(["hs-retail"], union, [], [], 4, ["hs-forever", "hs-idle"]);
        Assert.Equal(plain.Replace("progressObservations = {} },\n", "progressObservations = {}, foreverSources = { \"hs-forever\" } },\n", StringComparison.Ordinal), labelled);
    }
}

[Collection(DatabaseCollection.Name)]
public sealed class ForeverSyncTests : IDisposable
{
    private const string Protocol4 = "## Version: 0.3.7\n## X-Hourstone-Sync-Protocol: 4\n";
    private readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone.ForeverSync.Tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<CompanionService> services = [];
    private string Cloud => Path.Combine(root, "Cloud");
    private CompanionService Service(string name)
    {
        var service = new CompanionService(Path.Combine(root, name, "state.db"), name); services.Add(service); return service;
    }
    private SourceConfiguration Source(string pc, string client, string account, string sourceId, string guid, string toc = Protocol4)
    {
        var wow = Path.Combine(root, pc, "WoW");
        var source = Sample.Source(sourceId) with { WoWRoot = wow, ClientDirectory = Path.Combine(wow, client), AccountName = account };
        Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!); File.WriteAllText(source.AddonTocPath, toc);
        Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!);
        File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, Sample.Item(sourceId, guid: guid), 3));
        return source;
    }
    private static void Select(CompanionService service, params SourceConfiguration[] sources) =>
        service.SaveConfiguration(service.GetConfiguration() with { Sources = [.. sources] });
    private static async Task Sync(CompanionService device)
    {
        var result = await device.SyncNowAsync();
        Assert.True(result.Success, string.Join("; ", result.Issues.Select(issue => issue.Code + ": " + issue.Message)));
    }
    private static async Task SyncAll(params CompanionService[] devices)
    {
        for (var round = 0; round < 2; round++) foreach (var device in devices) await Sync(device);
    }
    private static string Data(SourceConfiguration source) => File.ReadAllText(Path.Combine(source.DataAddonDirectory, "Data.lua"));
    /// <summary>The list that every scope of a client's generated data carries; empty when the data has none.</summary>
    private static List<string> Listed(SourceConfiguration source)
    {
        var lists = ForeverSourcesTests.Lists(Data(source));
        if (lists.Count == 0) return [];
        Assert.All(lists, list => Assert.Equal(lists[0], list));
        return lists[0];
    }
    private static string Identity(CompanionService service, string guid) => ObservationRules.Identity(service.GetCharacters().Single(c => c.Guid == guid));

    [Fact]
    public async Task AnotherAccountsForeverCharactersAreListedForRetailAndForever()
    {
        var service = Service("A");
        var retail = Source("A", "_retail_", "RETAIL", "hs-retail", "Player-1-RR");
        var forever = Source("A", "_forever_", "FOREVER", "hs-forever", "Player-1-FF");
        var second = Source("A", "_forever_", "SECOND", "hs-forever-2", "Player-1-F2");
        Select(service, retail, forever, second); await Sync(service);
        Assert.Equal(["hs-forever", "hs-forever-2"], Listed(retail));
        Assert.Equal(["hs-forever", "hs-forever-2"], Listed(forever));
        Assert.Equal(["hs-forever", "hs-forever-2"], service.GetForeverSources().Order(StringComparer.Ordinal));
        Assert.Equal(new[] { Identity(service, "Player-1-FF"), Identity(service, "Player-1-F2") }.Order(StringComparer.Ordinal), service.GetForeverIdentities().Order(StringComparer.Ordinal));
        // The flavor, keys and observations stay Retail; only the list is added.
        Assert.Contains("flavor=\"retail\", guid=\"Player-1-FF\"", Data(retail), StringComparison.Ordinal);
    }
    [Fact]
    public async Task OtherPCsReceiveTheListThroughClientHints()
    {
        var forever = Service("A"); var retail = Service("B");
        var foreverSource = Source("A", "_forever_", "FOREVER", "hs-A", "Player-1-AA"); Select(forever, foreverSource);
        var retailSource = Source("B", "_retail_", "RETAIL", "hs-B", "Player-1-BB"); Select(retail, retailSource);
        foreach (var device in new[] { forever, retail }) await device.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, retail);
        Assert.Equal(["hs-A"], Listed(retailSource)); Assert.Equal(["hs-A"], Listed(foreverSource));
        Assert.Equal([Identity(retail, "Player-1-AA")], retail.GetForeverIdentities());
        // A PC that publishes no hint, like Companion 0.2.0, counts as another client.
        var hint = Path.Combine(forever.GetConfiguration().CloudFolder!, forever.GetConfiguration().DeviceId + ClientHints.Extension);
        File.Delete(hint); await Sync(retail);
        Assert.Empty(Listed(retailSource)); Assert.Empty(retail.GetForeverIdentities());
        await SyncAll(forever, retail); Assert.Equal(["hs-A"], Listed(retailSource));
        // Deselecting the Forever installation withdraws its characters and the list.
        Select(forever, foreverSource with { Enabled = false }); await SyncAll(forever, retail);
        Assert.Empty(Listed(retailSource)); Assert.DoesNotContain("Player-1-AA", Data(retailSource), StringComparison.Ordinal);
        Select(forever, foreverSource); await SyncAll(forever, retail); Assert.Equal(["hs-A"], Listed(retailSource));
        retail.DetachSyncFolder(); await Sync(retail);
        Assert.Empty(Listed(retailSource)); Assert.Empty(retail.GetForeverSources());
    }
    [Fact]
    public async Task AForeverCopyYieldsToTheOtherClient()
    {
        // A WTF folder copied from Retail on PC C into Forever on PC A keeps C's source ID.
        var forever = Service("A"); var viewer = Service("B"); var original = Service("C");
        var copy = Source("A", "_forever_", "COPY", "hs-copied", "Player-1-AA"); Select(forever, copy);
        var retailSource = Source("B", "_retail_", "RETAIL", "hs-B", "Player-1-BB"); Select(viewer, retailSource);
        Select(original, Source("C", "_retail_", "ORIGINAL", "hs-copied", "Player-1-CC"));
        foreach (var device in new[] { forever, viewer, original }) await device.JoinSyncFolderAsync(Cloud);
        await SyncAll(forever, viewer, original);
        Assert.Contains(viewer.GetCharacters(), c => c.Guid == "Player-1-AA");
        Assert.Empty(Listed(retailSource)); Assert.Empty(viewer.GetForeverIdentities());
        Assert.Empty(Listed(copy)); Assert.Empty(forever.GetForeverSources());
        // The hint itself still names the copy; readers apply the rule.
        Assert.Contains("hs-copied", File.ReadAllText(Path.Combine(forever.GetConfiguration().CloudFolder!, forever.GetConfiguration().DeviceId + ClientHints.Extension)), StringComparison.Ordinal);
    }
    [Fact]
    public async Task OlderAddonsReceiveFormatThreeWithoutTheList()
    {
        var service = Service("A");
        var retail = Source("A", "_retail_", "RETAIL", "hs-retail", "Player-1-RR", "## Version: 0.3.1\n");
        var forever = Source("A", "_forever_", "FOREVER", "hs-forever", "Player-1-FF");
        Select(service, retail, forever); await service.SyncNowAsync();
        Assert.StartsWith("-- Generated by Hourstone Companion. Changes will be replaced.\nHourstoneSync = { formatVersion = 3,", Data(retail), StringComparison.Ordinal);
        Assert.DoesNotContain("foreverSources", Data(retail), StringComparison.Ordinal);
        Assert.Equal(["hs-forever"], Listed(forever));
    }
    public void Dispose()
    {
        foreach (var service in services) service.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.ForeverSync.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
