using System;
namespace Hourstone.Companion.App;

public static class UpdatePolicy
{
    public static bool CanApply(bool syncBusy, bool windowVisible, bool windowActive, bool modalOpen, DateTimeOffset lastInteraction, DateTimeOffset noticeAt, DateTimeOffset now, bool wowRunning)
        => !syncBusy && !modalOpen && (!windowVisible || !windowActive) && !wowRunning
        && now - lastInteraction > TimeSpan.FromSeconds(90) && now - noticeAt > TimeSpan.FromSeconds(30);
}
