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
        var profile = RenderProfile.Parse(["--removed", "--selection-column", "3", "--selection-unfocused"]);
        Assert.True(profile.Selected); Assert.Equal(3, profile.SelectionColumn); Assert.True(profile.SelectionUnfocused);
        Assert.Null(profile.ClientsState);
    }

    [Theory]
    [InlineData("--clients-state")]
    [InlineData("--clients-state", "--english")]
    [InlineData("--clients-state", "ready")]
    [InlineData("--selection-column", "1")]
    [InlineData("--selection-unfocused")]
    [InlineData("--selected", "--selection-column", "4")]
    [InlineData("--selected", "--selection-column", "-1")]
    [InlineData("--selected", "--clients-state", "empty")]
    public void InvalidProfilesFailInsteadOfSavingAnUnrelatedScreenshot(params string[] arguments) =>
        Assert.Throws<ArgumentException>(() => RenderProfile.Parse(arguments));
}
