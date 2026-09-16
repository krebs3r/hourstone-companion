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
}
