using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class CharacterVisibilityViewTests
{
    [Fact]
    public void RemovedListNeverAddsRemovedTimeToOverviewAndSupportsFilters()
    {
        var all = MainViewModel.DemoData(); var model = new MainViewModel(false);
        model.SetObservations(all.Skip(2), all.Take(2));
        var time = model.TotalTime;
        Assert.Equal(6, model.CharacterCount); Assert.Equal(6, model.Rows.Count);
        Assert.Contains("(2)", model.ToggleRemovedLabel);
        model.ShowRemoved = true;
        Assert.Equal(2, model.Rows.Count); Assert.Equal(time, model.TotalTime);
        Assert.Equal("Gelöschte Charaktere", model.ListTitle);
        Assert.Equal("Wiederherstellen", model.CharacterActionLabel);
        model.Search = all[0].Name; Assert.Equal(all[0].Guid, Assert.Single(model.Rows).Value.Guid);
        model.SelectedRow = model.Rows[0]; Assert.True(model.CanChangeCharacter);
        model.IsIdle = false; Assert.False(model.CanChangeCharacter);
        model.IsIdle = true; Assert.True(model.CanChangeCharacter);
        model.SetLanguage(true);
        Assert.True(model.ShowRemoved); Assert.Single(model.Rows);
        Assert.Equal("Deleted characters", model.ListTitle); Assert.Equal("Restore character", model.CharacterActionLabel);
        Assert.Equal(all[0].Guid, model.SelectedRow!.Value.Guid);
        model.SetHours(false); model.SetLight(true);
        Assert.Equal(2, model.RemovedObservations.Count); Assert.Equal(6, model.CharacterCount);
        model.Search = ""; model.ShowRemoved = false; Assert.Equal(6, model.Rows.Count);
        Assert.Null(model.SelectedRow); Assert.False(model.CanChangeCharacter);
    }

    [Fact]
    public void RefreshAndRestoreKeepListModeAndCorrectTotalsWithoutMutatingInputs()
    {
        var all = MainViewModel.DemoData(); var model = new MainViewModel(false);
        model.SetObservations(all.Skip(1), all.Take(1)); model.ShowRemoved = true;
        model.SelectedRow = model.Rows.Single();
        model.SetObservations(all.Skip(1), all.Take(1));
        Assert.NotNull(model.SelectedRow); Assert.True(model.ShowRemoved);
        model.SetObservations(all, []);
        Assert.Empty(model.Rows); Assert.Null(model.SelectedRow); Assert.Equal(8, model.CharacterCount);
        Assert.Equal(model.Text("RemovedEmpty"), model.EmptyMessage);
        Assert.Equal(8, all.Count); model.ShowRemoved = false; Assert.Equal(8, model.Rows.Count);
    }

    [Fact]
    public void DuplicateNameWithDifferentGuidRemainsIndependentlySelectable()
    {
        var first = MainViewModel.DemoData()[0]; var second = first with { Guid = "Player-1-NEW-SYNTHETIC" };
        var model = new MainViewModel(false); model.SetObservations([second], [first]);
        Assert.Equal(second.Guid, Assert.Single(model.Rows).Value.Guid);
        model.ShowRemoved = true; Assert.Equal(first.Guid, Assert.Single(model.Rows).Value.Guid);
        Assert.Contains("/reload", model.Text("RemovalHint"));
    }
}
