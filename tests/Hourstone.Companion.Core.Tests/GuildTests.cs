using System.Text.Json;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class GuildTests
{
    [Fact]
    public void SharedInvalidGuildCasesAreRejectedByCore()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "contract-v2.json")));
        foreach (var test in document.RootElement.GetProperty("invalidCases").EnumerateArray())
        {
            var error = Record.Exception(() =>
            {
                var observation = JsonSerializer.Deserialize<Observation>(test.GetProperty("observation").GetRawText(), JsonContract.Options)!;
                ObservationRules.Validate(observation);
            });
            Assert.True(error is JsonException or InvalidDataException, test.GetProperty("name").GetString());
        }
    }
    [Theory]
    [InlineData(null, null)]
    [InlineData("", 0d)]
    [InlineData("Hüter der Morgenröte", 1700000000d)]
    public void MembershipStatesRoundTripThroughLiteralSavedVariables(string? guild, double? stamp)
    {
        var source = Sample.Source("hs-a"); var item = Sample.Item("hs-a") with { Guild = guild, GuildUpdatedAt = stamp };
        var row = Assert.Single(SavedVariablesReader.Read(Sample.Lua(source, item), source).Observations);
        Assert.Equal(guild, row.Guild); Assert.Equal(stamp, row.GuildUpdatedAt);
    }
    [Fact]
    public void LegacySavedVariablesCannotInventGuildMetadata()
    {
        var source = Sample.Source("hs-a"); var item = Sample.Item("hs-a") with { Guild = "Old extension", GuildUpdatedAt = 1 };
        var row = Assert.Single(SavedVariablesReader.Read(Sample.Lua(source, item, schema: 1), source).Observations);
        Assert.Null(row.Guild); Assert.Null(row.GuildUpdatedAt);
    }
    [Fact]
    public void GuildNameUsesUtf8ByteLimitAndTimestampMustBeFinite()
    {
        var item = Sample.Item("hs-a") with { Guild = new string('ä', 64), GuildUpdatedAt = 0 };
        ObservationRules.Validate(item);
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(item with { Guild = new string('ä', 65) }));
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1d, 9007199254740992d })
            Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(item with { GuildUpdatedAt = invalid }));
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(item with { Guild = null }));
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(item with { GuildUpdatedAt = null }));
        Assert.Throws<InvalidDataException>(() => ObservationRules.Validate(item with { Guild = "line\nbreak" }));
    }
    [Fact]
    public void GuildMergeIsAssociativeAndDoesNotMutateItsInputs()
    {
        var a = Sample.Item("hs-a", 220) with { ServerAt = 1700000100, ServerSeconds = 200, UpdatedAt = 1700000120, Guild = "Old", GuildUpdatedAt = 100 };
        var b = Sample.Item("hs-b") with { Guild = "New", GuildUpdatedAt = 300 };
        var c = Sample.Item("hs-c") with { Guild = "", GuildUpdatedAt = 400 };
        foreach (var sequence in new[] { new[] { a, b, c }, new[] { a, c, b }, new[] { b, a, c }, new[] { b, c, a }, new[] { c, a, b }, new[] { c, b, a } })
        {
            var direct = Assert.Single(ObservationRules.Merge(sequence));
            var grouped = Assert.Single(ObservationRules.Merge(ObservationRules.Merge(sequence.Take(2)).Concat(sequence.Skip(2))));
            Assert.Equal(direct, grouped); Assert.Equal(220, direct.Seconds); Assert.Equal(a.ServerAt, direct.ServerAt);
            Assert.Equal("", direct.Guild); Assert.Equal(400, direct.GuildUpdatedAt);
        }
        Assert.Equal("Old", a.Guild); Assert.Equal("New", b.Guild); Assert.Equal(120, c.Seconds);
    }
    [Fact]
    public void LegacyCanonicalSnapshotRetainsExactOriginalHash()
    {
        var snapshot = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-a")) with { FormatVersion = 1 };
        var json = ObservationRules.CanonicalSnapshot(snapshot);
        Assert.Equal(Environment.NewLine == "\r\n" ? "fbfab8912eeb42952c429af0691a6c8b765c1ea229dfe456b9cb69805578d20c" : "54b37ed4d9189f1a6c7e0b660e75d00c0d3541301270ddaf64ff5c8df581a862", SafeFiles.Sha256(json));
        Assert.Equal(json, ObservationRules.CanonicalSnapshot(ObservationRules.ParseSnapshot(json, snapshot.GroupId)));
        Assert.DoesNotContain("guild", json);
        Assert.Throws<InvalidDataException>(() => ObservationRules.ValidateSnapshot(snapshot with { Observations = [snapshot.Observations[0] with { Guild = "", GuildUpdatedAt = 0 }] }, snapshot.GroupId));
    }
    [Fact]
    public void GeneratedGuildDataIsQuotedAndExistingOwnershipRemainsStable()
    {
        var item = Sample.Item("hs-a") with { Guild = "L'été \\ \"Dämmerung\"", GuildUpdatedAt = 1700000400 };
        var data = DataAddonWriter.BuildData(["hs-a"], [item]);
        Assert.Contains("formatVersion = 3", data);
        Assert.Contains("guild=\"L'été \\\\ \\\"Dämmerung\\\"\"", data);
        Assert.Contains("guildUpdatedAt=1700000400", data);
        Assert.Equal("Hourstone Companion managed data addon v1", DataAddonWriter.OwnershipMarker);
    }
}
