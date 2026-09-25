# Hourstone Companion – notes for Store reviewers

Product version: 0.2.0. Target: Windows 11 x64, self-contained WPF desktop app. The MSIX package version is separate from the product version. Review the production-identity package supplied for submission, not the local-test certificate or identity.

Candidate: Store package **1.0.1.0**, product **0.2.0**, Store ID `9N9J1P7PKQTJ`, publisher **Martin Krebs Software**. This candidate addresses the 21 September 2026 failure under 10.6.3. The report uses a placeholder for the capability name, so attribution to `unvirtualizedResources` remains an inference.

Changes for reconsideration: removed `unvirtualizedResources` and the HKCU Run exclusion; the Store edition no longer removes/restores legacy startup entries. Users manually disable the previous edition's startup and confirm the handover. Support contact: **mail@martin-krebs.eu**. Developer website: https://github.com/krebs3r/hourstone-companion. Issue tracker: https://github.com/krebs3r/hourstone-companion/issues.

The previous submitted package and portal notes are archived under `artifacts/archive/20260924-before-certification-fix/`. Current validation and portal status are recorded in [the remediation record](CERTIFICATION-FIX-2026-09-24.md). This document provides detailed review instructions. Package 1.0.1.0 was resubmitted on 25 September at 19:36 UTC; the exact 4,820-character portal text is recorded in `artifacts/store-submission/reviewer-notes-submitted-20260925.txt`. Certification approval remains pending.

## Purpose and access

Hourstone Companion displays saved character playtime and Retail progress from the separately installed “Hourstone – Azeroth Hours” WoW addon. It can exchange observations between the user's own PCs through a folder managed by a file synchronization service. There is no Hourstone account, hosted sync server, paid unlock, test login or developer-provided credential. The application opens without WoW or a cloud service. Actual collection of a user's game data requires WoW and the addon; the synthetic test below exercises the normal file-reading and display path without a game account.

On first Store launch, complete the setup screen before normal synchronization starts. With no prior direct-edition profile, choose a fresh setup; optional startup can remain off. With an existing profile, the app offers transfer or fresh setup. A running direct copy must be closed through its notification-area **Quit / Beenden** action before handover proceeds.

Before confirming transfer or fresh setup, disable the previous edition in Windows startup settings or uninstall it. The setup confirmation documents this manual step; the Store app does not alter the old startup entry.

The app supports German and English and dark/light/system appearance under **Settings / Einstellungen**. Changes take effect through **Save changes / Änderungen speichern**. Closing the window keeps the application in the notification area. Open it there or choose **Quit / Beenden** to stop it. No administrator prompt or Windows service is required for normal use.

## Restricted capabilities and startup

`runFullTrust` is required for the existing WPF desktop process. It reads selected local WoW addon files, maintains local SQLite data, watches selected folders and writes its managed `Hourstone_Sync` data addon. It does not inject into the game or execute the Lua input. It runs at the user's normal integrity level.

The candidate does not request `unvirtualizedResources` and contains no registry virtualization exclusions. It does not modify the direct edition's startup entry. When an existing profile or startup entry is detected, setup opens Windows startup settings on request and requires the user's manual-handover confirmation. A remaining Run value is not interpreted as proof of enabled startup. The previous edition must be closed before synchronization begins.

Store startup remains optional and uses `HourstoneCompanionStartup`. Windows user/policy-disabled states are respected. A startup activation runs in the notification area; normal launch opens the window. The Store app runs without elevation.

## Data and update behavior

The Store profile uses the package's LocalFolder. A transfer uses a SQLite backup, validates a staging copy, retains the original profile and a backup, and creates a new device ID. Only one active copy should synchronize or write the generated addon. Transfer is offered before watchers and normal synchronization start; failure must leave the previous profile usable.

Snapshots contain selected character names, identifiers, realm, guild, playtime, Retail progress, device display name/identifiers and visibility decisions. They are readable files in the chosen folder; the app does not add encryption. The device name initially uses the Windows computer name. No cloud credentials or Battle.net passwords are requested. No source SavedVariables are overwritten. See [privacy.md](privacy.md) for the complete data description.

The packaged Store branch does not initialize Velopack or the GitHub updater. **Open Microsoft Store / Microsoft Store öffnen** opens the product page configured for a production identity; a local test package opens the Store updates page instead. The separate direct edition retains its GitHub updater. Addon download links open CurseForge in the default browser; addon installation and updates are separate from app updates.

## Reproducible test without WoW or a cloud account

The source repository also provides [`tools/verify-progress-roundtrip.ps1`](../../tools/verify-progress-roundtrip.ps1)
and its [execution instructions](../../tools/verify-progress-roundtrip/README.md).
It creates two isolated synthetic profiles and separate transport directories,
then imports the actual generated data into the Lua code from addon 0.3.1 and
0.3.2. The recorded run passes 38 Companion checks and ten Lua checkpoints,
including weekly reset, newer empty/lower values, damaged snapshots and no
republication of foreign observations. This checks the data pipeline locally;
it does not exercise native WoW APIs, a real cloud provider or a physical second PC.

Use an isolated Windows 11 x64 test user or VM. Install the candidate MSIX through the authorized test/submission route and launch it normally. A self-signed local test certificate is not a production submission credential. Keep any real WoW processes closed for this file-writing test.

The following PowerShell creates a new, uniquely named directory under the test user's temporary folder. It contains only a synthetic addon metadata file and synthetic SavedVariables; no game executable is needed. The historical timestamps deliberately produce outdated progress.

```powershell
$reviewRoot = Join-Path ([IO.Path]::GetTempPath()) ('HourstoneStoreReview-' + [guid]::NewGuid().ToString('N'))
$reviewClient = Join-Path $reviewRoot '_retail_'
$reviewSaved = Join-Path $reviewClient 'WTF\Account\SYNTHETIC\SavedVariables'
$reviewAddon = Join-Path $reviewClient 'Interface\AddOns\Hourstone'
New-Item -ItemType Directory -Path $reviewSaved, $reviewAddon | Out-Null
"## Version: 0.3.2`n## X-Hourstone-Sync-Protocol: 4`n" | Set-Content -LiteralPath (Join-Path $reviewAddon 'Hourstone.toc') -Encoding utf8
@'
HourstoneDB = {
  version=3, sourceId="store-review-local", visibility={},
  characters={one={sourceId="store-review-local",region="eu",flavor="retail",
    guid="Player-1-STORE-REVIEW",name="SampleMage",realm="SampleRealm",class="MAGE",
    level=80,seconds=7200,updatedAt=1700000000,serverSeconds=7200,serverAt=1700000000,
    guild="Sample Guild",guildUpdatedAt=1700000000}},
  progress={version=1,characters={one={sourceId="store-review-local",region="eu",
    flavor="retail",guid="Player-1-STORE-REVIEW",
    keystone={present=true,mapID=42,level=10,name="Sample Dungeon",updatedAt=1700000000,resetAt=1700604800},
    weekly={level=11,seasonID=1,updatedAt=1700000000,resetAt=1700604800},
    vault={rows={dungeon={updatedAt=1700000000,resetAt=1700604800,slots={
      {progress=4,threshold=1,level=10,difficultyName="Mythic+",unlocked=true},
      {progress=4,threshold=4,level=10,difficultyName="Mythic+",unlocked=true},
      {progress=4,threshold=8,level=0,unlocked=false}
    }}}}
  }}}
}
'@ | Set-Content -LiteralPath (Join-Path $reviewSaved 'Hourstone.lua') -Encoding utf8
Write-Output $reviewRoot
```

1. In **Clients**, use **Add WoW folder / WoW-Ordner hinzufügen** and select the printed directory. The synthetic source should become available. In **Overview / Übersicht**, expect SampleMage, SampleRealm, Sample Guild and two hours of playtime.
2. Open **Progress / Fortschritt** and select SampleMage. Expect key +10, weekly best +11, three captured Dungeon slots marked outdated, and unknown Raid/World rows. No invented current-week values should appear. Confirm search, realm filtering, language and theme changes.
3. Hide the character, confirm its removal from both views, then restore it through **Deleted characters / Gelöschte Charaktere**. This is reversible display control, not removal from WoW.
4. Inspect `<printed directory>\_retail_\Interface\AddOns\Hourstone_Sync\Data.lua` outside the package. Expect `formatVersion = 4` and the synthetic progress. The input `Hourstone.lua` must remain unchanged.
5. For local folder exchange, select a new empty directory under the same isolated test root on **Synchronization / Synchronisierung**. Inspect the generated `HourstoneSync\group.json` and device JSON using Explorer. The snapshot should contain only this synthetic source, plus device/visibility metadata. The plain local folder tests file exchange logic; it does not demonstrate an actual cloud upload.
6. Pause exchange, change the synthetic input's `seconds` to 7300 and `updatedAt` to 1700000100, save, then choose **Sync data / Daten abgleichen**. Local playtime should advance while the folder snapshot remains unchanged. Resume and sync to publish the new local value.
7. In **Synchronization → Local diagnostics**, inspect the local source and folder status (also available without a shared folder). In Settings, confirm the Store update card and absence of a GitHub update action. Close to the notification area, reopen, then quit explicitly. Settings and the synthetic source should survive a subsequent launch.

Synthetic input tests do not replace a native WoW run, Store installation validation, startup activation, transfer from a direct installation, or a real second-PC/cloud-provider test. The actual outcomes and remaining gaps are recorded in [submission-checklist.md](submission-checklist.md); submission does not mark them passed.

## Screenshots and remaining review evidence

German and English screenshots are actual WPF captures under `artifacts/store-submission/screenshots/`, using synthetic character data. They demonstrate the app's UI, including Store setup/settings, rather than an installed Store approval. Use the corresponding language with [description.de.md](description.de.md) and [description.en.md](description.en.md).

The [historical native LocalTest report](../MSIX-NATIVE-VALIDATION.md) applies to an earlier application DLL. It is not evidence for this candidate. The current [remediation record](CERTIFICATION-FIX-2026-09-24.md) lists exact artifacts and completed checks. Synthetic tests do not establish a real cloud transfer or native WoW run. Artwork rights remain as previously documented; this certification report did not cite them as a failure reason.
