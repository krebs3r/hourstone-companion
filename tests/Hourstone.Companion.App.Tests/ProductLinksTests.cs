using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Hourstone.Companion.App;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class ProductLinksTests
{
    [Theory]
    [InlineData(ProductLink.AddonCurseForge, "https://www.curseforge.com/wow/addons/hourstone-azeroth-hours")]
    [InlineData(ProductLink.AddonGitHub, "https://github.com/krebs3r/hourstone-azeroth-hours")]
    [InlineData(ProductLink.CompanionGitHub, "https://github.com/krebs3r/hourstone-companion")]
    public void ProductActionsUseOnlyTheExactApprovedDestinationInDefaultBrowser(ProductLink link, string expected)
    {
        ProcessStartInfo? requested = null;
        var result = ProductLinks.Open(link, info => requested = info);
        Assert.True(result.Succeeded); Assert.Null(result.TechnicalError); Assert.NotNull(requested);
        Assert.Equal(expected, requested.FileName); Assert.True(requested.UseShellExecute); Assert.Equal("open", requested.Verb);
        Assert.Equal("", requested.Arguments); Assert.Empty(requested.ArgumentList);
    }

    [Fact]
    public void UnknownActionCannotLaunchAnArbitraryDestination()
    {
        var called = false;
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductLinks.Open((ProductLink)999, _ => called = true));
        Assert.False(called);
    }

    [Theory]
    [InlineData("association")]
    [InlineData("access")]
    [InlineData("security")]
    [InlineData("state")]
    public void BrowserFailuresReturnDetailWithoutEscapingTheClickHandler(string failure)
    {
        Exception exception = failure switch
        {
            "association" => new Win32Exception(1155, "Synthetic browser association missing"),
            "access" => new UnauthorizedAccessException("Synthetic access denied"),
            "security" => new SecurityException("Synthetic policy rejection"),
            _ => new InvalidOperationException("Synthetic launch unavailable")
        };
        var result = ProductLinks.Open(ProductLink.AddonCurseForge, _ => throw exception);
        Assert.False(result.Succeeded); Assert.Equal(exception.Message, result.TechnicalError);
    }

    [Theory]
    [InlineData(LocalSourceReadiness.AddonMissing, "InstallAddonOnCurseForge")]
    [InlineData(LocalSourceReadiness.AddonOutdated, "UpdateAddonOnCurseForge")]
    [InlineData(LocalSourceReadiness.AwaitingGameSave, null)]
    [InlineData(LocalSourceReadiness.Ready, null)]
    [InlineData(LocalSourceReadiness.ReadFailed, null)]
    public void SourceAcquisitionActionMatchesTheActualReadiness(LocalSourceReadiness readiness, string? expected) =>
        Assert.Equal(expected, ProductLinks.AddonActionKey(readiness));

    [Theory]
    [InlineData(false, "Standardbrowser", "Installiere", "aktualisieren")]
    [InlineData(true, "default browser", "Install", "Update")]
    public void AcquisitionGuidanceAndBrowserRecoveryAreLocalized(bool english, string browserWord, string installWord, string updateWord)
    {
        var vm = new MainViewModel(false); vm.SetLanguage(english);
        Assert.Empty(vm.Rows); // Guidance is available before any local source or observation exists.
        Assert.Contains(AddonReadiness.MinimumAddonVersion, vm.Text("AddonSetupSteps"));
        Assert.Contains(installWord, vm.Text("AddonSetupSteps")); Assert.Contains("/reload", vm.Text("AddonSetupSteps"));
        Assert.Contains(updateWord, vm.Text("UpdateAddonOnCurseForge"));
        var url = ProductLinks.Address(ProductLink.AddonCurseForge);
        var failure = string.Format(vm.Culture, vm.Text("BrowserOpenFailed"), url);
        Assert.Contains(browserWord, failure); Assert.Contains(url, failure);
        Assert.Contains("CurseForge", vm.Text("AddonDownload")); Assert.Contains("GitHub", vm.Text("CompanionGitHubTooltip"));
    }
}
