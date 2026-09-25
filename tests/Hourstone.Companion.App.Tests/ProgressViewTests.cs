using Hourstone.Companion.Core;
using System.Windows;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class ProgressViewTests
{
    const long Now = 2_000_000_000;
    static Observation Character(string guid, string name, string flavor = "retail", string realm = "Antonidas") => new()
    {
        Guid = guid, Name = name, Flavor = flavor, Realm = realm, SourceId = "hs-local", Region = "eu", Class = "MAGE", Level = 90, Seconds = 3600, UpdatedAt = Now
    };
    static ProgressObservation Progress(Observation character, bool empty = false, long reset = Now + 1000) => new()
    {
        SourceId = character.SourceId, Region = character.Region, Flavor = "retail", Guid = character.Guid,
        Keystone = new() { Present = !empty, Level = empty ? null : 10, MapID = empty ? null : 1, Name = empty ? null : "Test Dungeon", UpdatedAt = Now - 100, ResetAt = reset },
        Weekly = new() { Level = empty ? 0 : 11, SeasonID = 1, UpdatedAt = Now - 100, ResetAt = reset },
        Vault = new() { Rows = new() { Dungeon = new() { UpdatedAt = Now - 100, ResetAt = reset, Slots = new long[] { 1, 4, 8 }.Select(t => new VaultSlot { Threshold = t, Progress = empty ? 0 : 4, Level = 10 }).ToList() } } }
    };
    [Fact]
    public void OnlyPlayedRetailCharactersAndTheirRealmsCanEnterProgress()
    {
        var vm = new ProgressViewModel(() => Now);
        var retail = Character("Player-1-A", "Aria");
        var classic = Character("Player-1-A", "Aria", "era", "ClassicRealm");
        vm.SetCharacters([retail, classic]); vm.SetProgress([Progress(retail)]);
        Assert.Equal("+10", Assert.Single(vm.Rows).Keystone);
        Assert.DoesNotContain("ClassicRealm", vm.Realms);
        vm.Search = "ClassicRealm"; Assert.Empty(vm.Rows);
        vm.Search = ""; Assert.Single(vm.Rows);
        vm.SetCharacters([classic]); Assert.Empty(vm.Rows); Assert.Equal("Noch keine Retail-Charaktere vorhanden.", vm.EmptyMessage);
    }
    [Fact]
    public void MissingProgressIsNotAZeroAndHiddenCharactersCannotBeResurrectedByProgress()
    {
        var vm = new ProgressViewModel(() => Now);
        var unknown = Character("Player-1-A", "Unknown"); var hidden = Character("Player-1-B", "Hidden");
        vm.SetCharacters([unknown]); vm.SetProgress([Progress(hidden)]);
        var row = Assert.Single(vm.Rows); Assert.Equal("Unknown", row.Name); Assert.Equal("Unbekannt", row.Keystone);
        Assert.All(row.Dungeons.Concat(row.Raids).Concat(row.World), slot => { Assert.False(slot.Known); Assert.Equal("?", slot.Symbol); });
        Assert.Contains("Noch keine Fortschrittsdaten", row.DetailsHint);
    }
    [Fact]
    public void EmptyExpiredAndFutureStatesRemainDistinctPerFamily()
    {
        var character = Character("Player-1-A", "Aria"); var vm = new ProgressViewModel(() => Now); vm.SetCharacters([character]);
        vm.SetProgress([Progress(character, empty: true)]);
        var row = Assert.Single(vm.Rows); Assert.Equal("Keiner", row.Keystone); Assert.Equal("Keiner", row.Weekly);
        Assert.All(row.Dungeons, slot => { Assert.True(slot.Known); Assert.False(slot.IsStale); Assert.Equal("·", slot.Symbol); });
        var stale = Progress(character, reset: Now - 10);
        vm.SetProgress([stale with { Weekly = stale.Weekly! with { UpdatedAt = Now + 10, ResetAt = Now + 100 } }]);
        row = Assert.Single(vm.Rows); Assert.Equal("+10", row.Keystone); Assert.Equal("Veraltet", row.KeystoneWarning); Assert.Equal("Veraltet", row.WeeklyWarning);
        Assert.All(row.Dungeons, slot => Assert.True(slot.IsStale)); Assert.All(row.Raids, slot => Assert.False(slot.Known));
    }
    [Fact]
    public void FilteringAndRemovalClearDetailsAndDoNotChangePlaytimeFilters()
    {
        var app = new MainViewModel(false);
        var first = Character("Player-1-A", "Aria"); var second = Character("Player-1-B", "Borin", realm: "Blackhand");
        app.SetObservations([first, second, Character("Player-1-C", "Classic", "era", "Everlook")]);
        app.SelectedClient = "Classic Era"; app.Search = "Classic";
        var vm = app.Progress; Assert.Equal(2, vm.Rows.Count); vm.Selected = vm.Rows[0]; Assert.Equal(Visibility.Visible, vm.DetailsVisibility);
        vm.SelectedRealm = "Blackhand"; Assert.Null(vm.Selected); Assert.Single(vm.Rows); Assert.Single(app.Rows); Assert.Equal(3, app.CharacterCount);
        vm.Selected = vm.Rows[0]; app.SetObservations([first]); Assert.Null(vm.Selected); Assert.Equal(vm.AllRealms, vm.SelectedRealm);
    }
    [Fact]
    public void SourceScopedUnknownRegionsNeverAttachAnotherAccountsProgress()
    {
        var one = Character("Player-1-A", "Aria") with { Region = "unknown" };
        var two = one with { SourceId = "hs-other" };
        var vm = new ProgressViewModel(() => Now); vm.SetCharacters([one]); vm.SetProgress([Progress(two)]);
        Assert.Equal("Unbekannt", Assert.Single(vm.Rows).Keystone);
    }
    [Fact]
    public void LanguageAndThemeRefreshKeepValidSelectionAndDoNotInventLocalizedDungeonNames()
    {
        var character = Character("Player-1-A", "Aria"); var vm = new ProgressViewModel(() => Now);
        vm.SetCharacters([character]); vm.SetProgress([Progress(character)]); vm.Selected = vm.Rows[0];
        var dark = vm.Rows[0].Dungeons[0].Background;
        vm.SetAppearance(true, true);
        Assert.NotNull(vm.Selected); Assert.Equal("Test Dungeon", vm.Selected.KeystoneName); Assert.Equal("All realms", vm.SelectedRealm);
        Assert.Equal("Unlocked", vm.Selected.Dungeons[0].Status); Assert.NotEqual(dark, vm.Selected.Dungeons[0].Background);
        Assert.Equal(3, vm.Selected.Dungeons.Count); Assert.Equal(3, vm.Selected.Raids.Count); Assert.Equal(3, vm.Selected.World.Count);
    }
}
