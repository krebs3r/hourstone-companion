using Hourstone.Companion.App;
using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class SyncNoticeTrackerTests
{
    [Fact]
    public void RepeatedPollingAndReorderedIssuesDoNotAnnounceAgain()
    {
        var tracker = new SyncNoticeTracker();
        SyncIssue a = new("source_not_initialized", "First save", "account-a");
        SyncIssue b = new("source_not_initialized", "First save", "account-b");
        Assert.True(tracker.Update([a, b]));
        Assert.False(tracker.Update([a, b]));
        Assert.False(tracker.Update([b, a]));
        Assert.True(tracker.Update([a]));
    }
    [Fact]
    public void RecoveryRearmsTheSameProblemAndNewDetailsAreAnnounced()
    {
        var tracker = new SyncNoticeTracker();
        SyncIssue issue = new("source_read_failed", "Locked", "account-a");
        Assert.True(tracker.Update([issue]));
        Assert.True(tracker.Update([issue with { Message = "Missing" }]));
        Assert.False(tracker.Update([]));
        Assert.False(tracker.Update([]));
        Assert.True(tracker.Update([issue]));
    }
}
