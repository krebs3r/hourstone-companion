# Hourstone Companion 0.1.3

Characters can be removed from the overview without deleting their saved playtime
or their WoW character. Select an entry and choose **Remove from overview**;
confirmation explains the effect on totals. **Removed characters** lists hidden
entries and provides **Restore character**. Both lists retain search and filters.
Selected rows now keep readable theme colors in dark and light appearance.

Removing or restoring an entry synchronizes across personal devices. A fresh WoW
login also restores it after the addon has received the removal. Reloads, ongoing
sessions and stale files do not restore it. An unseen concurrent removal wins;
receive it first, then restore explicitly or perform a new login. All controls
preserve their acknowledgment history and do not depend on PC clocks.

Update all PCs to Companion **0.1.3** and all selected WoW clients to Hourstone
**0.2.2**. Snapshot format 3 and SavedVariables schema 3 introduce the controls;
older supported data migrates without losing settings or measurements. Old app
versions cannot consume the new format and must be upgraded together.

Automated coverage includes merge order and concurrency, stale and malformed
inputs, restart persistence, transport filtering and removed-list totals. Render
checks cover dark/light/system appearance, compact windows and scaling. Actual
WoW logins and Dropbox/OneDrive exchange between two physical PCs remain practical
release checks. Local test packages are unsigned; public releases require signing.
