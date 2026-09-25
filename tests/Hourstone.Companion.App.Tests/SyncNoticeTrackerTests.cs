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
    [Fact]
    public void ChangingOnlyTheAffectedFileAnnouncesTheNewProblem()
    {
        var tracker = new SyncNoticeTracker();
        var issue = new SyncIssue("cloud_file_not_local", "File is not locally available.") { FilePath = @"C:\Cloud\device-a.json" };
        Assert.True(tracker.Update([issue]));
        Assert.False(tracker.Update([issue]));
        Assert.True(tracker.Update([issue with { FilePath = @"C:\Cloud\device-b.json" }]));
        Assert.False(tracker.Update([]));
        Assert.True(tracker.Update([issue]));
    }
}
