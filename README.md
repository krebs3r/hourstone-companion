# Hourstone Companion

A Windows companion for [Hourstone – Azeroth Hours](https://github.com/krebs3r/hourstone-azeroth-hours).
View saved character playtime across supported WoW clients and synchronize your
own PCs through a locally available Dropbox or OneDrive folder.

[Download Hourstone Companion for Windows](https://github.com/krebs3r/hourstone-companion/releases/latest)

Choose the **Setup.exe** installer or fully extract the **Portable.zip** package.
The .NET runtime is included. **Version 0.1.6 is an unsigned early release.**
End-to-end Dropbox and OneDrive exchange between two PCs has not yet been
practically validated. See the [release notes](docs/RELEASE-NOTES.md).

![Hourstone Companion with sample characters](docs/assets/overview.png)

## What it does

- Character overview with guilds, total playtime, character/guild search, client and realm filters.
- Reversible deletion from the overview, synchronized across your own PCs.
- Local, read-only ingestion of selected Hourstone account data.
- Optional device synchronization using one snapshot per computer and no companion server.
- A generated data addon makes the combined overview available in WoW.
- Windows tray operation, configurable autostart, German/English and dark/light/system themes.
- Clear save feedback, per-account setup guidance and original class and game icons.

It supports **Windows 11 x64** and Hourstone **0.2.2 or later** with Retail, Mists Classic,
TBC Anniversary and Classic Era. The WoW addon also works independently.

## Setup

1. Use **Download addon on CurseForge** on the **Clients** page, or open
   [Hourstone on CurseForge](https://www.curseforge.com/wow/addons/hourstone-azeroth-hours). Install or update to 0.2.2 or later.
   Enable Hourstone, log in with each account and log out or run `/reload` once so the addon saves its source identifier.
2. Choose **Find installations** and select your WoW installations and account sources.
3. Restart WoW once after the companion first creates the `Hourstone_Sync` addon.
   Later updates become available on login or reload.
4. For multiple computers, follow **Synchronization** below to connect a locally
   available Dropbox, OneDrive or other synchronized folder.

The download button opens the official CurseForge page in your default browser;
installation is handled there. It is available before you configure any sources,
and missing or outdated addons show the same action beside their setup instructions.
**Addon on GitHub** links to the addon source; **GitHub** in the footer opens this
companion repository. The footer also shows the version and **with ♥ by krebs3r**.

![Addon setup with a synthetic source](docs/assets/clients.png)

The companion requires a compatible addon release. Check the version on CurseForge:
a published Hourstone 0.1.x package cannot provide companion synchronization.
If CurseForge still lists an older version, use the compatible
[Hourstone 0.2.3 addon release on GitHub](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.2.3).
CurseForge remains the preferred addon update source after approval.

Guilds appear beneath each character. **No guild** is a confirmed state;
**Not yet recorded** means that character still needs a login with Hourstone 0.2.1
or later, followed by logout or `/reload`. The last saved guild is shown while
the character is offline. Guild changes synchronize independently of playtime.
Update the companion to 0.1.3 or later on every PC before exchanging protocol 3
snapshots. Earlier snapshots and existing settings and databases remain readable.

Use the trash icon to the right of a character's last update to exclude it from
the list and its total, after confirmation. No row selection is required. The WoW
character and saved playtime are kept. **Deleted characters** opens the list with
a **Restore character** icon in the same position on each row.

![Deleted characters with sample data](docs/assets/removed-characters.png)

A fresh WoW login also restores the character, once the removal has reached that
addon. A `/reload`, app restart or old file from an offline laptop does not restore
it. Removing the character you are currently playing keeps it hidden until the
next login; time measurement continues. Both apps exchange these decisions
alongside saved measurements. Characters deleted inside WoW remain in Hourstone
until you delete their entries from the overview; renaming a character with the same GUID preserves
its entry and time.

In **Settings**, changing the device name, appearance, language or autostart enables
**Save changes**. The highlighted button applies the choices; the unsaved status
clears after a successful save. Closing to the tray keeps the current draft.

![Settings with unsaved changes in light appearance](docs/assets/settings.png)

If an account is waiting for its first save, log in **inside that WoW client** with
Hourstone 0.2.2, then log out or run `/reload`. Restarting the Companion alone is
not enough. The Clients page identifies each account that still needs this step.

WoW must save, the provider must transport the files, and the target client must
load the data. The companion reports these stages separately. It does not claim
that a cloud upload has finished or another computer is online.

The companion never overwrites WoW SavedVariables and never executes their Lua
contents. It keeps its SQLite database, settings and device identity locally.
Only selected character observations and the matching removal/restoration controls
enter the provider folder. Learned controls stay locally after disconnecting;
unrelated old-group identities are not published into a new group.

## Synchronization

![Synchronization setup with sample data](docs/assets/synchronization.png)

Hourstone writes exchange files to a local folder. Dropbox, OneDrive or another
file synchronization service transfers that folder to your other computers; their
Companion apps then import the files automatically. Sign in to your chosen service,
not to Hourstone. No Companion account or hosted Hourstone server is needed.

1. On the first PC, choose a folder already synchronized by your service.
   Hourstone creates an **HourstoneSync** subfolder there.
2. On another PC, wait until that subfolder and its contents have arrived, then
   select the same synchronized folder or **HourstoneSync** itself. This joins
   the existing group instead of creating a separate one in an empty folder.
3. Keep **HourstoneSync** available offline on every PC, and keep both Companion
   and the synchronization service running for exchange. Use Dropbox's offline
   availability option or OneDrive's **Always keep on this device** option.

**Sync data** reads the last saved Hourstone files of all selected accounts,
imports available data from other PCs, updates the overview and writes the combined
data for the WoW addon. The same cycle runs at startup, after file changes and every
30 seconds. The button does not make WoW save and does not force a cloud transfer.
Save new playtime by logging out or using `/reload` in WoW first.

**Last sync check** is the time of the completed check, not the last playtime change.
Files in the sync folder do not confirm that another PC has received them.
WoW loads the prepared overview at its next login or `/reload`; after the first
creation of **Hourstone_Sync**, restart WoW completely once. The reload that caused
WoW to save may finish before the Companion has prepared the new overview.

Without a shared folder, local processing still works. **Pause** stops folder
exchange while keeping local processing and received data. The current folder
path and separate folder/WoW status messages are shown on **Synchronization**.

## Application updates

Companion checks the public stable releases of this repository at startup and
every 24 hours while running. No GitHub login is required; prereleases are excluded.
**Check for app updates** performs the same check immediately and downloads an
available update. Releases must contain the Velopack feed and update packages;
a source commit, tag or standalone installer is not an update feed.

After download, the app announces the update. It waits until WoW is closed, the
window is inactive, at least 90 seconds have passed without input and at least
30 seconds have passed since the notice. Open dialogs, synchronization, saving and
unsaved settings prevent installation. The app restarts in the background, retaining
its settings and data. Update errors leave local processing available.

Installed copies and fully extracted Velopack portable packages support updates.
Unpackaged development builds do not. **The WoW addon is updated separately through
CurseForge.**

## Build and test

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
for Windows x64. The repository pins its SDK and dependency versions.

```powershell
dotnet restore Hourstone.Companion.slnx --locked-mode
dotnet build Hourstone.Companion.slnx -c Release --no-restore
dotnet test Hourstone.Companion.slnx -c Release --no-build
dotnet run --project src/Hourstone.Companion.App -- --demo
pwsh -File tools/render-smoke.ps1
pwsh -File tools/package.ps1 -Unsigned
```

Local package builds are unsigned by default in the example above. Public unsigned
releases require an explicit publishing option; signed releases remain the default.
End-user builds include the .NET runtime; users do not need an SDK. See [Development](docs/DEVELOPMENT.md),
[Release validation](docs/RELEASING.md) and the
[pinned synchronization contract](docs/sync-protocol-v3.md).

## Project boundaries

This first version does not provide daily/weekly statistics, global deletion,
direct provider APIs, a hosted account service, or macOS/Linux builds. Disconnecting
a sync folder removes received contributions locally and does not delete files on
other computers.

Source code is [MIT licensed](LICENSE). Original Blizzard images have separate
[asset notices and provenance](docs/ASSETS.md). Dependency and asset notices are included in packages.
Hourstone is an independent project and is not affiliated with Blizzard Entertainment.
