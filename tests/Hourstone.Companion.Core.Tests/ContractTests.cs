using System.Text.Json;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class ContractTests
{
    [Fact]
    public void ExactSharedLuaContractFixturesAgree()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "contract-v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var items = JsonSerializer.Deserialize<List<Observation>>(test.GetProperty("observations").GetRawText(), JsonContract.Options)!;
            var actual = ObservationRules.Merge(items);
            Assert.True(actual.Count == test.GetProperty("expectedCount").GetInt32(), test.GetProperty("name").GetString());
            Assert.Equal(test.GetProperty("expectedSeconds").EnumerateArray().Select(n => n.GetDouble()).Order(), actual.Select(o => o.Seconds).Order());
            if (test.TryGetProperty("expectedNames", out var names)) Assert.Equal(names.EnumerateArray().Select(n => n.GetString()).Order(), actual.Select(o => o.Name).Order());
        }
    }
    [Fact]
    public void ExactGoldenSnapshotRoundTripsWithNoLocalPaths()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"));
        using var document = JsonDocument.Parse(json);
        var snapshot = ObservationRules.ParseSnapshot(json, document.RootElement.GetProperty("groupId").GetString()!);
        var output = ObservationRules.CanonicalSnapshot(snapshot);
        Assert.Equal(output, ObservationRules.CanonicalSnapshot(ObservationRules.ParseSnapshot(output, snapshot.GroupId)));
        Assert.DoesNotContain("clientDirectory", output); Assert.DoesNotContain("savedVariablesPath", output); Assert.DoesNotContain("accountName", output);
    }
    [Theory]
    [InlineData("return {}")]
    [InlineData("HourstoneDB = os.execute('touch bad')")]
    [InlineData("HourstoneDB = {}; doAnything()")]
    [InlineData("HourstoneDB = { version=2, version=1, characters={} }")]
    [InlineData("HourstoneDB = { version=999, characters={} }")]
    [InlineData("HourstoneDB = { version=2, characters={")]
    [InlineData("HourstoneDB = { version=2, characters={}, settings=setmetatable({}, {}) }")]
    [InlineData("HourstoneDB = { version=2, characters={}, evil=1e999 }")]
    public void RejectsExecutableMalformedAndFutureLua(string lua) => Assert.ThrowsAny<Exception>(() => SavedVariablesReader.Read(lua, Sample.Source("hs-test")));
    [Fact]
    public void ReadsSchemaOneWithoutInventingConfirmation()
    {
        var source = Sample.Source("hs-test"); var parsed = SavedVariablesReader.Read(Sample.Lua(source, Sample.Item("hs-test"), schema: 1), source);
        Assert.Null(parsed.SourceId); Assert.False(Assert.Single(parsed.Observations).Confirmed);
    }
    [Fact]
    public void ReadsSchemaTwoAndSkipsImportedEntries()
    {
        var source = Sample.Source("hs-test"); var lua = Sample.Lua(source, Sample.Item("hs-test"));
        var parsed = SavedVariablesReader.Read(lua, source); Assert.Equal("hs-test", parsed.SourceId); Assert.True(Assert.Single(parsed.Observations).Confirmed);
        var imported = lua.Replace("seconds=120", "imported=true, seconds=120", StringComparison.Ordinal);
        Assert.Empty(SavedVariablesReader.Read(imported, source).Observations);
    }
    [Fact]
    public void InvalidConfirmedBaselineIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(Sample.Item("hs-test") with { Seconds = 1 }));
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(Sample.Item("hs-test") with { UpdatedAt = 1 }));
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(Sample.Item("hs-test") with { ServerAt = null }));
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(Sample.Item("hs-test") with { Seconds = double.PositiveInfinity }));
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(Sample.Item("hs-test") with { Name = new string('界', 43) }));
    }
    [Fact]
    public void JSONMissingFieldsDuplicatePropertiesAndUnknownVersionsAreRejected()
    {
        var snapshot = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-test"));
        var json = ObservationRules.CanonicalSnapshot(snapshot);
        Assert.ThrowsAny<Exception>(() => ObservationRules.ParseSnapshot(json.Replace("\"formatVersion\": 1,", ""), snapshot.GroupId));
        Assert.ThrowsAny<Exception>(() => ObservationRules.ParseSnapshot(json.Replace("\"formatVersion\": 1,", "\"formatVersion\": 1, \"formatVersion\": 1,"), snapshot.GroupId));
        Assert.ThrowsAny<Exception>(() => ObservationRules.ParseSnapshot(json.Replace("\"formatVersion\": 1", "\"formatVersion\": 99"), snapshot.GroupId));
    }
    [Fact]
    public void DataAddonIsPureDataWithoutSavedVariablesAndEscapesStrings()
    {
        var text = DataAddonWriter.BuildData(["hs-local"], [Sample.Item("hs-test") with { Name = "Quote\"Back\\" }]);
        Assert.StartsWith("-- Generated", text); Assert.Contains("HourstoneSync =", text); Assert.Contains("Quote\\\"Back\\\\", text); Assert.DoesNotContain("SavedVariables", text);
    }
    [Fact]
    public void SourceTraversalIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => SourceDiscovery.Validate(Sample.Source("hs-test") with { AccountName = ".." }));
        Assert.Throws<InvalidDataException>(() => SourceDiscovery.Validate(Sample.Source("hs-test") with { ClientDirectory = Path.GetPathRoot(Path.GetTempPath())! }));
    }
}

internal static class Sample
{
    public static SourceConfiguration Source(string id) => new() { SourceId = id, WoWRoot = Path.Combine(Path.GetTempPath(), "HourstoneTests"), ClientDirectory = Path.Combine(Path.GetTempPath(), "HourstoneTests", "_retail_"), AccountName = "TEST", Region = "eu", Flavor = "retail" };
    public static Observation Item(string sourceId, double seconds = 120, string guid = "Player-1-AB") => new()
    { SourceId = sourceId, Region = "eu", Flavor = "retail", Guid = guid, Name = "Testheld", Realm = "Testrealm", Class = "MAGE", Level = 80, Seconds = seconds, UpdatedAt = 1700000030, ServerSeconds = 100, ServerAt = 1700000000 };
    public static DeviceSnapshot Snapshot(string deviceId, long revision, params Observation[] observations) => new()
    { GroupId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", DeviceId = deviceId, DeviceName = "Test PC", Revision = revision, Observations = [.. observations] };
    public static string Lua(SourceConfiguration source, Observation observation, int schema = 2)
    {
        var rootId = schema >= 2 ? $"sourceId={DataAddonWriter.Quote(source.SourceId)}," : "";
        var server = schema >= 2 && observation.Confirmed ? $", serverSeconds={observation.ServerSeconds!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, serverAt={observation.ServerAt!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : "";
        return $$"""
            -- synthetic test data only
            HourstoneDB = { version={{schema}}, {{rootId}} characters = {
              ["{{source.Flavor}}:{{observation.Guid}}"]={ guid={{DataAddonWriter.Quote(observation.Guid)}}, name={{DataAddonWriter.Quote(observation.Name)}}, realm={{DataAddonWriter.Quote(observation.Realm)}}, class="MAGE", flavor={{DataAddonWriter.Quote(source.Flavor)}}, region="eu", level=80, seconds={{observation.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, updatedAt={{observation.UpdatedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}}{{server}} }
            }, settings={ nested={true,false,nil}, text=[=[literal -- text]=] } }
            """;
    }
}
