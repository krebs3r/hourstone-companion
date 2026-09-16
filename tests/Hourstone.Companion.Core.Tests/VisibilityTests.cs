using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class VisibilityTests
{
    private static CharacterVisibility State(string source = "hs-a") => VisibilityRules.FromObservation(Sample.Item(source));
    private static string Json(CharacterVisibility state) => JsonSerializer.Serialize(state, JsonContract.Options);

    [Fact]
    public void SharedLuaVisibilityFixturesAgree()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "visibility-v3.json")));
        foreach (var test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var states = JsonSerializer.Deserialize<List<CharacterVisibility>>(test.GetProperty("states").GetRawText(), JsonContract.Options)!;
            var expected = JsonSerializer.Deserialize<CharacterVisibility>(test.GetProperty("expected").GetRawText(), JsonContract.Options)!;
            var actual = Assert.Single(VisibilityRules.Merge(states));
            Assert.Equal(Json(Assert.Single(VisibilityRules.Merge([expected]))), Json(actual));
            Assert.Equal(test.GetProperty("hidden").GetBoolean(), VisibilityRules.IsRemoved(actual));
        }
        foreach (var test in document.RootElement.GetProperty("invalid").EnumerateArray())
            Assert.ThrowsAny<Exception>(() => VisibilityRules.Validate(JsonSerializer.Deserialize<CharacterVisibility>(test.GetProperty("state").GetRawText(), JsonContract.Options)!));
    }

    [Fact]
    public void RestoreAcknowledgesOnlyObservedRemovesAndConcurrentRemoveWins()
    {
        var first = VisibilityRules.Remove(State(), "actor-a");
        var restored = VisibilityRules.Restore(first);
        var concurrent = VisibilityRules.Remove(first, "actor-b");
        var merged = Assert.Single(VisibilityRules.Merge([restored, concurrent]));
        Assert.True(VisibilityRules.IsRemoved(merged)); Assert.Equal(1, merged.Restored["actor-a"]);
        Assert.False(merged.Restored.ContainsKey("actor-b"));
        Assert.False(VisibilityRules.IsRemoved(VisibilityRules.Restore(merged)));
        Assert.True(VisibilityRules.IsRemoved(VisibilityRules.Remove(VisibilityRules.Restore(merged), "actor-a")));
    }

    [Fact]
    public void MergeIsAssociativeCommutativeIdempotentAndDoesNotMutateInputs()
    {
        var a = VisibilityRules.Remove(State("hs-a"), "actor-a");
        var b = VisibilityRules.Restore(a) with { SourceId = "hs-b" };
        var c = VisibilityRules.Remove(State("hs-c"), "actor-c");
        var original = Json(a); var expected = Json(Assert.Single(VisibilityRules.Merge([a, b, c])));
        foreach (var permutation in new[] { new[] { a, b, c }, [a, c, b], [b, a, c], [b, c, a], [c, a, b], [c, b, a] })
        {
            Assert.Equal(expected, Json(Assert.Single(VisibilityRules.Merge(permutation))));
            Assert.Equal(expected, Json(Assert.Single(VisibilityRules.Merge(VisibilityRules.Merge(permutation.Take(2)).Concat(permutation.Skip(2))))));
            Assert.Equal(expected, Json(Assert.Single(VisibilityRules.Merge(permutation.Take(1).Concat(VisibilityRules.Merge(permutation.Skip(1)))))));
            Assert.Equal(expected, Json(Assert.Single(VisibilityRules.Merge(permutation.Concat(permutation)))));
        }
        var output = Assert.Single(VisibilityRules.Merge([a])); output.Removed["actor-a"] = 99;
        Assert.Equal(original, Json(a)); Assert.Equal("hs-c", Assert.Single(VisibilityRules.Merge([a, b, c])).SourceId);
    }

    [Fact]
    public void UnknownRegionStatesDoNotAffectOtherSourcesWithTheSameGuid()
    {
        var a = VisibilityRules.Remove(State("hs-a") with { Region = "unknown" }, "actor-a");
        var b = State("hs-b") with { Region = "unknown" };
        var merged = VisibilityRules.Merge([a, b]); Assert.Equal(2, merged.Count);
        Assert.False(VisibilityRules.IsRemoved(merged.Single(state => state.SourceId == "hs-b")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9007199254740992L)]
    public void InvalidRemoveSequencesAreRejected(long sequence) =>
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Validate(State() with { Removed = new() { ["actor-a"] = sequence } }));

    [Fact]
    public void RestoreCannotAcknowledgeUnseenOrFutureSequence()
    {
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Validate(State() with { Restored = new() { ["actor-a"] = 1 } }));
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Validate(State() with { Removed = new() { ["actor-a"] = 1 }, Restored = new() { ["actor-a"] = 2 } }));
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Remove(State(), "invalid:actor"));
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Validate(State() with { Removed = null! }));
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Validate(State() with { Guid = "bad\nidentity" }));
    }

    [Fact]
    public void ActorAndCounterLimitsFailWithoutMutatingExistingState()
    {
        var full = State() with { Removed = Enumerable.Range(0, VisibilityRules.MaximumActors).ToDictionary(n => "actor-" + n, _ => 1L) };
        VisibilityRules.Validate(full);
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Remove(full, "new-actor"));
        Assert.Equal(VisibilityRules.MaximumActors, full.Removed.Count);
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Merge([full, VisibilityRules.Remove(State(), "new-actor")]));
        var exhausted = State() with { Removed = new() { ["actor-a"] = VisibilityRules.MaximumSequence } };
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Remove(exhausted, "actor-a"));
        Assert.False(VisibilityRules.IsRemoved(VisibilityRules.Restore(exhausted)));
    }

    [Fact]
    public void TotalStateLimitIsEnforcedAcrossMergedInputs()
    {
        var maximum = Enumerable.Range(0, VisibilityRules.MaximumStates).Select(n => State() with { Guid = "Player-" + n }).ToArray();
        Assert.Equal(VisibilityRules.MaximumStates, VisibilityRules.Merge(maximum).Count);
        Assert.Throws<InvalidDataException>(() => VisibilityRules.Merge(maximum.Append(State() with { Guid = "Player-extra" })));
    }

    [Fact]
    public void FormatThreeRequiresListAndOlderFormatsRejectVisibility()
    {
        var snapshot = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1, Sample.Item("hs-a"));
        Assert.Throws<InvalidDataException>(() => ObservationRules.ValidateSnapshot(snapshot with { FormatVersion = 3 }, snapshot.GroupId));
        foreach (var version in new[] { 1, 2 })
            Assert.Throws<InvalidDataException>(() => ObservationRules.ValidateSnapshot(snapshot with { FormatVersion = version, Visibility = [] }, snapshot.GroupId));
        var valid = snapshot with { FormatVersion = 3, Visibility = [] };
        Assert.Empty(ObservationRules.ParseSnapshot(ObservationRules.CanonicalSnapshot(valid), valid.GroupId).Visibility!);
        Assert.Throws<InvalidDataException>(() => ObservationRules.ValidateSnapshot(valid with { Visibility = [State(), State()] }, valid.GroupId));
    }

    [Fact]
    public void CanonicalVisibilitySortsStatesAndActorKeys()
    {
        var a = State() with { Removed = new() { ["z"] = 1, ["a"] = 2 }, Restored = new() { ["a"] = 1 } };
        var b = State() with { Guid = "Player-0-Z", Removed = new() { ["b"] = 3 } };
        var snapshot = Sample.Snapshot("11111111-1111-1111-1111-111111111111", 1) with { FormatVersion = 3, Visibility = [a, b] };
        var reordered = snapshot with { Visibility = [b, a with { Removed = new() { ["a"] = 2, ["z"] = 1 } }] };
        var canonical = ObservationRules.CanonicalSnapshot(snapshot);
        Assert.Equal(canonical, ObservationRules.CanonicalSnapshot(reordered));
        Assert.Equal(canonical, ObservationRules.CanonicalSnapshot(ObservationRules.ParseSnapshot(canonical, snapshot.GroupId)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void LegacyCanonicalHashDoesNotAcquireVisibilityOrLoseExplicitServerNulls(int version)
    {
        var legacy = $$"""
            {
              "formatVersion": {{version}},
              "groupId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "deviceId": "11111111-1111-1111-1111-111111111111",
              "deviceName": "Legacy PC",
              "revision": 41,
              "observations": [
                {
                  "sourceId": "hs-legacy",
                  "region": "eu",
                  "flavor": "retail",
                  "guid": "Player-1-AB",
                  "name": "Testheld",
                  "realm": "Testrealm",
                  "class": "MAGE",
                  "level": 80,
                  "seconds": 120,
                  "updatedAt": 1700000030,
                  "serverSeconds": null,
                  "serverAt": null
                }
              ]
            }
            """;
        legacy = legacy.ReplaceLineEndings(Environment.NewLine);
        var canonical = ObservationRules.CanonicalSnapshot(ObservationRules.ParseSnapshot(legacy, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        Assert.Equal(legacy, canonical);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(legacy)), SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        Assert.DoesNotContain("visibility", canonical);
    }

    [Theory]
    [InlineData("nil")]
    [InlineData("{ [2]={} }")]
    [InlineData("{ {sourceId='hs-a',region='eu',flavor='retail',guid='Player-1-AB',removed={a=1.5},restored={}} }")]
    [InlineData("{ {sourceId='hs-a',region='eu',flavor='retail',guid='Player-1-AB',removed={a=1},restored={a=2}} }")]
    [InlineData("{ {sourceId='hs-a',region='eu',flavor='retail',guid='Player-1-AB',removed={a=1,a=2},restored={}} }")]
    [InlineData("os.execute('no')")]
    public void SavedVariablesRejectInvalidVisibilityBeforeReturningObservations(string literal)
    {
        var source = Sample.Source("hs-a");
        var text = Sample.Lua(source, Sample.Item("hs-a"), 3).Replace("visibility={}", "visibility=" + literal, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => SavedVariablesReader.Read(text, source));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OlderSavedVariablesRejectUnexpectedVisibilityInsteadOfAcceptingNewSemantics(int schema)
    {
        var source = Sample.Source("hs-a");
        var text = Sample.Lua(source, Sample.Item("hs-a"), schema).Replace("characters =", "visibility={}, characters =", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => SavedVariablesReader.Read(text, source));
    }

    [Fact]
    public void WriterUsesPureFormatThreeDataAndKeepsHiddenObservationsForRestore()
    {
        var observation = Sample.Item("hs-a") with { Name = "Quoted\"Hero\\", Guild = "聯盟", GuildUpdatedAt = 123 };
        var state = VisibilityRules.Remove(VisibilityRules.FromObservation(observation), "runtime-actor");
        var text = DataAddonWriter.BuildData(["hs-route"], [observation], [state]);
        Assert.Contains("formatVersion = 3", text); Assert.Contains("visibility = {", text);
        Assert.Contains("Quoted\\\"Hero\\\\", text); Assert.Contains("[\"runtime-actor\"]=1", text);
        Assert.Contains("seconds=120", text); Assert.DoesNotContain("SavedVariables", text);
        Assert.Equal("Hourstone Companion managed data addon v1", DataAddonWriter.OwnershipMarker);
    }
}
