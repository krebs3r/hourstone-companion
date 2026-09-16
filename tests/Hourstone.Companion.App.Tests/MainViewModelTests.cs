using System.ComponentModel;
using System.Globalization;
using System.Windows;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class MainViewModelTests
{
    private static Observation Character(string guid, string name, string flavor = "retail", string realm = "Testrealm", double seconds = 3600) => new()
    {
        SourceId = "hs-synthetic",
        Region = "eu",
        Flavor = flavor,
        Guid = guid,
        Name = name,
        Realm = realm,
        Class = "MAGE",
        Level = 80,
        Seconds = seconds,
        UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
    private static MainViewModel Populated()
    {
        var model = new MainViewModel(false);
        model.SetObservations([
            Character("Player-1-A", "Äther", "retail", "Antonidas", 3600),
            Character("Player-1-B", "Borin", "era", "Everlook", 7200),
            Character("Player-1-C", "Aria", "retail", "Everlook", 10800)
        ]);
        return model;
    }
    [Fact]
    public void EmptyModelOffersSetupAndDoesNotContainDemoData()
    {
        var model = new MainViewModel(false);
        Assert.False(model.Demo); Assert.Empty(model.Rows); Assert.Equal(0, model.CharacterCount);
        Assert.Equal(Visibility.Visible, model.EmptyVisibility); Assert.Equal(Visibility.Visible, model.SearchHintVisibility);
        Assert.Equal(model.Text("EmptyText"), model.EmptyMessage); Assert.Equal(model.Text("Unconfigured"), model.Status);
        Assert.Equal("0 Std.", model.TotalTime); Assert.Single(model.Clients); Assert.Single(model.Realms);
    }
    [Fact]
    public void NoMatchesExplainsFiltersRatherThanSuggestingInitialSetup()
    {
        var model = Populated(); model.Search = "does-not-exist";
        Assert.Empty(model.Rows); Assert.Equal(Visibility.Visible, model.EmptyVisibility);
        Assert.Equal(model.Text("NoMatches"), model.EmptyMessage); Assert.Equal(Visibility.Collapsed, model.SearchHintVisibility);
        Assert.Equal(3, model.CharacterCount); Assert.Equal("6 Std.", model.TotalTime);
        model.Search = ""; Assert.Equal(3, model.Rows.Count); Assert.Equal(Visibility.Collapsed, model.EmptyVisibility);
    }
    [Fact]
    public void SearchClientAndRealmFiltersComposeWithoutMutatingAccountTotals()
    {
        var model = Populated(); model.SelectedClient = "Retail"; model.SelectedRealm = "Everlook"; model.Search = "aRI";
        Assert.Equal("Aria", Assert.Single(model.Rows).Name);
        Assert.Equal(3, model.CharacterCount); Assert.Equal(2, model.ClientCount); Assert.Equal("6 Std.", model.TotalTime);
        Assert.Equal("1 Charaktere · 1 Clients", model.RowSummary);
        model.Search = ""; model.SelectedRealm = model.Text("AllRealms"); Assert.Equal(2, model.Rows.Count);
        model.SelectedClient = model.Text("AllClients"); Assert.Equal(3, model.Rows.Count);
        model.Search = "äTH"; Assert.Equal("Äther", Assert.Single(model.Rows).Name);
    }
    [Fact]
    public void ResultsSortByPlaytimeAndThenByName()
    {
        var model = new MainViewModel(false); model.SetObservations([
            Character("Player-1-C", "Cedric", seconds: 7200), Character("Player-1-B", "Borin", seconds: 3600), Character("Player-1-A", "Aria", seconds: 3600)
        ]);
        Assert.Equal(new[] { "Cedric", "Aria", "Borin" }, model.Rows.Select(row => row.Name));
    }
    [Fact]
    public void SyncRefreshRetainsValidFiltersAndResetsUnavailableRealm()
    {
        var model = Populated(); model.SelectedClient = "Retail"; model.SelectedRealm = "Everlook";
        model.SetObservations([Character("Player-1-A", "Aria", "retail", "Everlook", 15000)]);
        Assert.Equal("Retail", model.SelectedClient); Assert.Equal("Everlook", model.SelectedRealm); Assert.Single(model.Rows);
        model.SetObservations([Character("Player-1-A", "Aria", "retail", "Antonidas", 18000)]);
        Assert.Equal("Retail", model.SelectedClient); Assert.Equal(model.Text("AllRealms"), model.SelectedRealm); Assert.Single(model.Rows);
    }
    [Fact]
    public void ChangingLanguageRetainsSpecificFiltersAndLocalizesAllFilters()
    {
        var model = Populated(); model.SelectedClient = "Retail"; model.SelectedRealm = "Everlook"; model.Search = "Ar";
        model.SetLanguage(true);
        Assert.True(model.English); Assert.Equal("en-US", model.Culture.Name); Assert.Equal("Retail", model.SelectedClient); Assert.Equal("Everlook", model.SelectedRealm);
        Assert.Equal("Aria", Assert.Single(model.Rows).Name); Assert.Equal("All clients", model.Clients[0]); Assert.Equal("All realms", model.Realms[0]);
        Assert.Equal("Every character. Every client. One overview.", model.T["Subtitle"]); Assert.Equal("6 hrs", model.TotalTime);
        model.SelectedClient = model.Text("AllClients"); model.SelectedRealm = model.Text("AllRealms"); model.Search = ""; model.SetLanguage(false);
        Assert.Equal("Alle Clients", model.SelectedClient); Assert.Equal("Alle Realms", model.SelectedRealm); Assert.Equal(3, model.Rows.Count);
    }
    [Fact]
    public void TimeFormatToggleRebuildsRowsWithoutResettingFilters()
    {
        var model = new MainViewModel(false); model.SetObservations([Character("Player-1-A", "Aria", seconds: 90061)]);
        model.SelectedClient = "Retail"; model.Search = "ari";
        Assert.Equal("25 Std. 01 Min.", Assert.Single(model.Rows).Time);
        model.SetHours(false); Assert.Equal("1 T. 1 Std.", Assert.Single(model.Rows).Time);
        Assert.Equal("Retail", model.SelectedClient); Assert.Equal("ari", model.Search);
        model.SetLanguage(true); Assert.Equal("1d 1h", Assert.Single(model.Rows).Time);
        model.SetHours(true); Assert.Equal("25 hrs 01 min", Assert.Single(model.Rows).Time);
    }
    [Fact]
    public void DeduplicatedServiceProjectionDisplaysOneWinningCharacterAndCorrectSum()
    {
        var older = Character("Player-1-A", "Aria", seconds: 3600) with { UpdatedAt = 1000 };
        var newer = older with { SourceId = "hs-other", Seconds = 7200, UpdatedAt = 2000 };
        var era = Character("Player-1-A", "AriaClassic", "era", seconds: 3600);
        var model = new MainViewModel(false); model.SetObservations(ObservationRules.Merge([older, newer, era]));
        Assert.Equal(2, model.CharacterCount); Assert.Equal("3 Std.", model.TotalTime);
        Assert.Equal(7200, model.Rows.Single(row => row.Name == "Aria").Seconds); Assert.Equal(2, model.ClientCount);
    }
    [Fact]
    public void ModelNotifiesBoundEmptyTotalAndLocalizationPropertiesAfterChanges()
    {
        var model = new MainViewModel(false); var properties = new List<string?>();
        model.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        model.SetObservations([Character("Player-1-A", "Aria")]);
        Assert.Contains(nameof(MainViewModel.TotalTime), properties); Assert.Contains(nameof(MainViewModel.CharacterCount), properties);
        Assert.Contains(nameof(MainViewModel.EmptyVisibility), properties); Assert.Contains(nameof(MainViewModel.EmptyMessage), properties);
        properties.Clear(); model.SetLanguage(true);
        Assert.Contains(nameof(MainViewModel.T), properties); Assert.Contains(nameof(MainViewModel.SelectedClient), properties); Assert.Contains(nameof(MainViewModel.TotalTime), properties);
    }
    [Fact]
    public void FutureObservationTimestampDisplaysJustNowWithoutNegativeAge()
    {
        var model = new MainViewModel(false); model.SetObservations([Character("Player-1-A", "Aria") with { UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400 }]);
        Assert.Equal("gerade eben", Assert.Single(model.Rows).Age); model.SetLanguage(true); Assert.Equal("just now", Assert.Single(model.Rows).Age);
    }
    [Theory]
    [InlineData("retail", "Retail", "retail.png")]
    [InlineData("mists", "Mists Classic", "mists.png")]
    [InlineData("tbc", "TBC Anniversary", "tbc.png")]
    [InlineData("era", "Classic Era", "era.png")]
    public void SupportedClientFamiliesHaveDistinctDisplayNames(string flavor, string label, string iconFile)
    {
        var model = new MainViewModel(false); model.SetObservations([Character("Player-1-A", "Aria", flavor)]);
        var row = Assert.Single(model.Rows); Assert.Equal(label, row.Client); Assert.EndsWith("/Clients/" + iconFile, row.ClientIconPath);
    }
}
