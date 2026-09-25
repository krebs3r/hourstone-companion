using System.Text.Json;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class ProgressTests
{
    internal static ProgressObservation Item(string source = "hs-a", long updated = 100, long level = 10) => new()
    {
        SourceId = source, Region = "eu", Flavor = "retail", Guid = "Player-1-A",
        Keystone = new() { Present = true, MapID = 42, Level = level, Name = "Dungeon", UpdatedAt = updated, ResetAt = updated + 100 },
        Weekly = new() { Level = level, SeasonID = 1, UpdatedAt = updated, ResetAt = updated + 100 }
    };
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonContract.Options);
    [Fact]
    public void SharedLuaFixtureAgreesAndSourceMergeIsAssociativeCommutativeIdempotent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "progress-v4.json")));
        foreach (var test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input = JsonSerializer.Deserialize<List<ProgressObservation>>(test.GetProperty("progress"), JsonContract.Options)!;
            var expected = JsonSerializer.Deserialize<List<ProgressObservation>>(test.GetProperty("expected"), JsonContract.Options)!;
            Assert.Equal(Json(expected), Json(ProgressRules.Project(input)));
            Assert.Equal(Json(expected), Json(ProgressRules.Project(input.AsEnumerable().Reverse())));
            var canonical = Json(ProgressRules.MergeSources(input));
            Assert.Equal(canonical, Json(ProgressRules.MergeSources(input.Concat(input))));
            Assert.Equal(canonical, Json(ProgressRules.MergeSources(input.AsEnumerable().Reverse())));
            for (var split = 0; split <= input.Count; split++)
                Assert.Equal(canonical, Json(ProgressRules.MergeSources(ProgressRules.MergeSources(input.Take(split)).Concat(ProgressRules.MergeSources(input.Skip(split))))));
        }
    }
    [Fact]
    public void FamilyProvenanceSurvivesIntermediateSourceMerges()
    {
        var a = Item("hs-a") with { Weekly = null };
        var b = Item("hs-z") with { Keystone = null };
        var c = Item("hs-b") with { Keystone = a.Keystone! with { Name = "Winning source" }, Weekly = null };
        var raw = ProgressRules.MergeSources(ProgressRules.MergeSources([a, b]).Append(c));
        Assert.Equal(3, raw.Count); Assert.Equal("Winning source", Assert.Single(ProgressRules.Project(raw)).Keystone!.Name);
    }
    [Fact]
    public void EmptyAndExpiredAndFutureTimestampsAreDistinctFromMissing()
    {
        var key = new KeystoneProgress { Present = false, UpdatedAt = 100, ResetAt = 200 };
        Assert.Equal(ProgressState.Known, ProgressRules.State(key, DateTimeOffset.FromUnixTimeSeconds(100)));
        Assert.Equal(ProgressState.Stale, ProgressRules.State(key, DateTimeOffset.FromUnixTimeSeconds(200)));
        Assert.Equal(ProgressState.Stale, ProgressRules.State(key, DateTimeOffset.FromUnixTimeSeconds(99)));
        Assert.Equal(ProgressState.Unknown, ProgressRules.State(null, DateTimeOffset.FromUnixTimeSeconds(150)));
        var projected = Assert.Single(ProgressRules.Project([Item(), Item(updated: 101) with { Keystone = key with { UpdatedAt = 101 }, Weekly = null }]));
        Assert.False(projected.Keystone!.Present); Assert.Equal(10, projected.Weekly!.Level);
    }
    [Fact]
    public void ValidationRejectsUnsafeNumbersTextIncompleteVaultAndNonRetail()
    {
        Assert.Throws<InvalidDataException>(() => ProgressRules.Validate(Item() with { Flavor = "era" }));
        Assert.Throws<InvalidDataException>(() => ProgressRules.Validate(Item() with { Guid = "bad\nidentity" }));
        Assert.Throws<InvalidDataException>(() => ProgressRules.Validate(Item() with { Keystone = Item().Keystone! with { MapID = 0 } }));
        Assert.Throws<InvalidDataException>(() => ProgressRules.Validate(Item() with { Weekly = Item().Weekly! with { Level = ProgressRules.MaximumNumber + 1 } }));
        Assert.Throws<InvalidDataException>(() => ProgressRules.Validate(Item() with { Keystone = Item().Keystone! with { Name = new string('界', 342) } }));
        Assert.Throws<InvalidDataException>(() => ProgressRules.Validate(Item() with { Weekly = Item().Weekly! with { ResetAt = 100 } }));
        Assert.Throws<InvalidDataException>(() => ProgressRules.Validate(new VaultRowProgress { UpdatedAt = 100, ResetAt = 200, Slots = [new() { Threshold = 1 }] }));
        Assert.Throws<InvalidDataException>(() => ProgressRules.MergeSources(Enumerable.Range(0, ProgressRules.MaximumObservations + 1).Select(i => Item() with { Guid = "Player-" + i })));
    }
    [Fact]
    public void CanonicalOrderUsesSeparateSourceAndIdentityAndNormalizesAbsentPayload()
    {
        var values = ProgressRules.MergeSources([Item("hs-a-b"), Item("hs-a") with { Keystone = new() { Present = false, UpdatedAt = 100, ResetAt = 200, MapID = 42, Level = 12, Name = "irrelevant" }, Vault = new() }]);
        Assert.Equal("hs-a", values[0].SourceId); Assert.Null(values[0].Keystone!.MapID); Assert.Null(values[0].Vault);
    }
    [Fact]
    public void VersionFourRequiresProgressAndLegacySerializationNeverGainsIt()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v4.json"));
        var snapshot = ObservationRules.ParseSnapshot(json, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Assert.Single(snapshot.ProgressObservations!);
        var canonical = ObservationRules.CanonicalSnapshot(snapshot);
        Assert.Equal(canonical, ObservationRules.CanonicalSnapshot(ObservationRules.ParseSnapshot(canonical, snapshot.GroupId)));
        Assert.Throws<InvalidDataException>(() => ObservationRules.ValidateSnapshot(snapshot with { ProgressObservations = null }, snapshot.GroupId));
        Assert.Throws<InvalidDataException>(() => ObservationRules.ValidateSnapshot(snapshot with { FormatVersion = 3 }, snapshot.GroupId));
        Assert.Throws<InvalidDataException>(() => ObservationRules.ValidateSnapshot(snapshot with { ProgressObservations = [Item(), Item()] }, snapshot.GroupId));
        Assert.ThrowsAny<Exception>(() => ObservationRules.ParseSnapshot(canonical.Replace("\"seasonID\": 1", "\"seasonID\": 1.5", StringComparison.Ordinal), snapshot.GroupId));
        Assert.ThrowsAny<Exception>(() => ObservationRules.ParseSnapshot(canonical.Replace("\"seasonID\": 1", "\"seasonID\": 1, \"unknown\": true", StringComparison.Ordinal), snapshot.GroupId));
        foreach (var version in new[] { 1, 2, 3 })
        {
            var legacy = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"snapshot-v{version}.json"));
            using var doc = JsonDocument.Parse(legacy);
            var parsed = ObservationRules.ParseSnapshot(legacy, doc.RootElement.GetProperty("groupId").GetString()!);
            Assert.Null(parsed.ProgressObservations); Assert.DoesNotContain("progress", ObservationRules.CanonicalSnapshot(parsed), StringComparison.OrdinalIgnoreCase);
        }
    }
    [Fact]
    public void DataAddonPreservesSourceRecordsAndExplicitLegacyFormat()
    {
        var raw = new[] { Item("hs-a"), Item("hs-b", 110) };
        var modern = DataAddonWriter.BuildData(["hs-local"], [Sample.Item("hs-local")], [], raw, 4);
        Assert.Contains("formatVersion = 4", modern); Assert.Contains("progressObservations", modern);
        Assert.Contains("[\"sourceId\"]=\"hs-a\"", modern); Assert.Contains("[\"sourceId\"]=\"hs-b\"", modern);
        var legacy = DataAddonWriter.BuildData(["hs-local"], [Sample.Item("hs-local")], [], raw, 3);
        Assert.Contains("formatVersion = 3", legacy); Assert.DoesNotContain("progressObservations", legacy);
    }
}
