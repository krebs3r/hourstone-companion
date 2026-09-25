using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class RenderProfileTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("missing")]
    [InlineData("outdated")]
    public void SetupStatesSelectOnlySyntheticClientPreviews(string state)
    {
        var profile = RenderProfile.Parse(["--clients-state", state, "--english"]);
        Assert.Equal(state, profile.ClientsState); Assert.False(profile.Selected);
    }

    [Fact]
    public void RemovedListAndLastColumnSelectionUseTheSameSelectionChecks()
    {
        var profile = RenderProfile.Parse(["--removed", "--selection-column", "4", "--selection-unfocused"]);
        Assert.True(profile.Selected); Assert.Equal(4, profile.SelectionColumn); Assert.True(profile.SelectionUnfocused);
        Assert.Null(profile.ClientsState);
    }

    [Theory]
    [InlineData("disconnected")]
    [InlineData("connected")]
    [InlineData("paused")]
    [InlineData("error")]
    [InlineData("unchanged")]
    public void SynchronizationStatesAreExplicitSyntheticScenarios(string state)
    {
        var profile = RenderProfile.Parse(["--sync-state", state, "--english"]);
        Assert.Equal(state, profile.SyncState);
        Assert.Null(profile.ClientsState);
        Assert.False(profile.Selected);
    }

    [Fact]
    public void CloudFileDiagnosticsUseAnExplicitSyntheticScenario()
    {
        var profile = RenderProfile.Parse(["--diagnostics-state", "cloud-file-not-local"]);
        Assert.Equal("cloud-file-not-local", profile.DiagnosticsState);
        Assert.Null(profile.SyncState);
        Assert.Null(profile.ClientsState);
        Assert.False(profile.Selected);
    }

    [Theory]
    [InlineData("--progress-details")]
    [InlineData("--store-state", "unknown")]
    [InlineData("--store-state", "found", "--selected")]
    [InlineData("--clients-state")]
    [InlineData("--clients-state", "--english")]
    [InlineData("--clients-state", "ready")]
    [InlineData("--selection-column", "1")]
    [InlineData("--selection-unfocused")]
    [InlineData("--selected", "--selection-column", "5")]
    [InlineData("--selected", "--selection-column", "-1")]
    [InlineData("--selected", "--clients-state", "empty")]
    [InlineData("--sync-state")]
    [InlineData("--sync-state", "online")]
    [InlineData("--sync-state", "connected", "--selected")]
    [InlineData("--sync-state", "connected", "--clients-state", "empty")]
    [InlineData("--diagnostics-state")]
    [InlineData("--diagnostics-state", "invalid")]
    [InlineData("--diagnostics-state", "cloud-file-not-local", "--sync-state", "connected")]
    [InlineData("--diagnostics-state", "cloud-file-not-local", "--clients-state", "empty")]
    [InlineData("--diagnostics-state", "cloud-file-not-local", "--selected")]
    public void InvalidProfilesFailInsteadOfSavingAnUnrelatedScreenshot(params string[] arguments) =>
        Assert.Throws<ArgumentException>(() => RenderProfile.Parse(arguments));
}
