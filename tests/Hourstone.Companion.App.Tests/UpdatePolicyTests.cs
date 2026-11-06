using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class UpdatePolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, 91, 31, false, true)]
    [InlineData(false, true, false, false, 91, 31, false, true)]
    [InlineData(false, false, true, false, 91, 31, false, true)]
    [InlineData(true, false, false, false, 91, 31, false, false)]
    [InlineData(false, true, true, false, 91, 31, false, false)]
    [InlineData(false, false, false, true, 91, 31, false, false)]
    [InlineData(false, false, false, false, 91, 31, true, false)]
    [InlineData(false, false, false, false, 90, 31, false, false)]
    [InlineData(false, false, false, false, 89, 31, false, false)]
    [InlineData(false, false, false, false, 91, 30, false, false)]
    [InlineData(false, false, false, false, 91, 29, false, false)]
    [InlineData(false, false, false, false, -1, 31, false, false)]
    [InlineData(false, false, false, false, 91, -1, false, false)]
    public void UpdatesRequireIdleAppSufficientNoticeAndNoRunningWoW(bool busy, bool visible, bool active, bool modal, int idleSeconds, int noticeSeconds, bool wow, bool expected)
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, UpdatePolicy.CanApply(busy, visible, active, modal, now.AddSeconds(-idleSeconds), now.AddSeconds(-noticeSeconds), now, wow));
    }
    [Fact]
    public void NewUserInteractionResetsIdleWaitingEvenWhenNoticeWasAlreadyShown()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
        var notice = now.AddMinutes(-10);
        Assert.True(UpdatePolicy.CanApply(false, true, false, false, now.AddSeconds(-91), notice, now, false));
        Assert.False(UpdatePolicy.CanApply(false, true, false, false, now, notice, now, false));
    }
    [Fact]
    public void QuotedIdleTimeIsTheTimeThePolicyWaits()
    {
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
        var notice = now.AddMinutes(-10);
        Assert.Equal(90, UpdatePolicy.IdleSeconds);
        Assert.False(UpdatePolicy.CanApply(false, false, false, false, now.AddSeconds(-UpdatePolicy.IdleSeconds), notice, now, false));
        Assert.True(UpdatePolicy.CanApply(false, false, false, false, now.AddSeconds(-UpdatePolicy.IdleSeconds - 1), notice, now, false));
    }
    [Theory]
    [InlineData(false, false, false, false, false, ManualInstallOutcome.Install)]
    [InlineData(true, false, false, false, false, ManualInstallOutcome.AfterSync)]
    [InlineData(false, false, true, false, false, ManualInstallOutcome.UnsavedSettings)]
    [InlineData(false, true, false, false, false, ManualInstallOutcome.UnsavedSettings)]
    [InlineData(true, false, true, false, false, ManualInstallOutcome.UnsavedSettings)]
    [InlineData(false, false, false, true, false, ManualInstallOutcome.ModalOpen)]
    [InlineData(true, true, true, true, false, ManualInstallOutcome.ModalOpen)]
    [InlineData(false, false, false, false, true, ManualInstallOutcome.CheckRunning)]
    [InlineData(true, true, true, true, true, ManualInstallOutcome.CheckRunning)]
    public void ExplicitInstallKeepsOnlySafetyConditionsAndWaitsForARunningSync(bool sync, bool saving, bool dirty, bool modal, bool checking, ManualInstallOutcome expected) =>
        Assert.Equal(expected, UpdatePolicy.ManualInstall(sync, saving, dirty, modal, checking));
    [Theory]
    [InlineData(null, "0.2.3", true)]
    [InlineData("0.2.3", "0.2.3", false)]
    [InlineData("0.2.3-Beta", "0.2.3-beta", false)]
    [InlineData("0.2.3", "0.2.4", true)]
    public void RepeatedCheckReplacesOnlyAnotherVersion(string? pending, string found, bool expected) =>
        Assert.Equal(expected, UpdatePolicy.ReplacesPending(pending, found));
    [Theory]
    [InlineData(false, "Update 0.2.3 ist bereit.", "90 Sekunden", "„Jetzt installieren“")]
    [InlineData(true, "Update 0.2.3 is ready.", "90 seconds", "Install now")]
    public void ReadyNoticeNamesVersionConditionsAndImmediateAction(bool english, string start, string idle, string action)
    {
        var text = UpdateCoordinator.ReadyNotice("0.2.3", english);
        Assert.StartsWith(start, text); Assert.Contains(idle, text); Assert.Contains(action, text); Assert.Contains("WoW", text);
        Assert.Contains(UpdatePolicy.IdleSeconds + " ", UpdateCoordinator.StatusMessage("Ready", english));
        var model = new MainViewModel(false); model.SetLanguage(english);
        Assert.Contains(UpdatePolicy.IdleSeconds + " ", model.Text("UpdateInfo")); Assert.Contains(model.Text("InstallNow"), model.Text("UpdateInfo"));
        Assert.Contains(model.Text("InstallNow"), text);
        Assert.Equal(english ? "Version 0.2.3 has been downloaded and is waiting to install." : "Version 0.2.3 ist heruntergeladen und wartet auf die Installation.",
            string.Format(model.Culture, model.Text("UpdatePending"), "0.2.3"));
    }
}
