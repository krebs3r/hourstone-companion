using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class SyncStatusViewTests
{
    [Fact]
    public void CheckTimestampTracksChecksWithoutChangingCharacterData()
    {
        var model = new MainViewModel(false);
        var character = new Observation
        {
            SourceId = "synthetic-source", Region = "eu", Flavor = "retail", Guid = "Player-1-A",
            Name = "ExampleMage", Realm = "ExampleRealm", Class = "MAGE", Level = 80,
            Seconds = 3600, UpdatedAt = 1_700_000_000
        };
        model.SetObservations([character]);
        Assert.Empty(model.LastSync);
        var first = new DateTimeOffset(2026, 9, 16, 12, 32, 0, TimeSpan.Zero);
        model.RecordCheck(first);
        var firstLabel = model.LastSync;
        model.RecordCheck(first.AddMinutes(1));
        Assert.NotEqual(firstLabel, model.LastSync);
        Assert.StartsWith("Letzter Abgleich · ", model.LastSync);
        Assert.EndsWith(first.AddMinutes(1).ToLocalTime().ToString("HH:mm", model.Culture), model.LastSync);
        Assert.Equal(character, Assert.Single(model.VisibleObservations));
        Assert.Equal(3600, Assert.Single(model.Rows).Seconds);
    }

    [Fact]
    public void LanguageRefreshKeepsLastCheckInstantAndPublishesItsNewLabel()
    {
        var model = new MainViewModel(false);
        var completed = new DateTimeOffset(2026, 9, 16, 12, 32, 0, TimeSpan.Zero);
        model.RecordCheck(completed);
        var changed = new List<string?>();
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        model.SetLanguage(true);
        Assert.Contains(nameof(MainViewModel.LastSync), changed);
        Assert.Equal("Last sync check · " + completed.ToLocalTime().ToString("HH:mm", model.Culture), model.LastSync);
        model.SetLanguage(false);
        Assert.Equal("Letzter Abgleich · " + completed.ToLocalTime().ToString("HH:mm", model.Culture), model.LastSync);
    }

    [Fact]
    public void AvailableCloudDataLabelsTheCheckTimeRatherThanAFileWriteTime()
    {
        var model = new MainViewModel(false);
        var completed = new DateTimeOffset(2026, 9, 16, 12, 32, 0, TimeSpan.Zero);
        var clock = completed.ToLocalTime().ToString("HH:mm", model.Culture);
        Assert.Equal("Daten im Syncordner verfügbar · Geprüft um " + clock, model.CloudAvailability(completed));
        model.SetLanguage(true);
        Assert.Equal("Data available in sync folder · Checked at " + clock, model.CloudAvailability(completed));
        Assert.EndsWith(completed.AddMinutes(1).ToLocalTime().ToString("HH:mm", model.Culture), model.CloudAvailability(completed.AddMinutes(1)));
    }

    [Theory]
    [InlineData("Busy")]
    [InlineData("NotInstalled")]
    [InlineData("NoNewerVersion")]
    [InlineData("Ready")]
    [InlineData("Unavailable")]
    public void UpdateFeedbackCanChangeLanguageWithoutStartingAnotherCheck(string key)
    {
        var german = UpdateCoordinator.StatusMessage(key, false);
        var english = UpdateCoordinator.StatusMessage(key, true);
        Assert.NotEqual(german, english);
        Assert.Equal(english, UpdateCoordinator.LocalizeStatus(german, true));
        Assert.Equal(german, UpdateCoordinator.LocalizeStatus(english, false));
        Assert.Equal("", UpdateCoordinator.LocalizeStatus("", true));
    }
}
