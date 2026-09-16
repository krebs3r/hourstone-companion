# Release validation

Public releases use `v<version>` tags in this repository. The application version
in `Directory.Build.props` must match the tag. Updates are sourced exclusively
from `krebs3r/hourstone-companion`; addon releases have their own version series.

## Automated checks

- Offline bundled-artwork verification with `python tools/verify_assets.py`: catalog,
  SHA-256, PNG structure and dimensions against `docs/assets-manifest.json`.
- Locked solution restore, Release build and core/conformance tests.
- Dark/light render smoke checks at 100%, 150% and 200%; validation CI also renders
  system theme, settings, all classes, long names, compact layouts, selected rows
  and addon acquisition states. Footer bounds and full-row selection are checked.
- Repository privacy guard across the current tree and newly introduced commit blobs.
- Self-contained win-x64 packaging, package content allowlist and checksums.
- For signed releases, valid Authenticode signatures for the installer and executable payloads.

A normal CI run creates unsigned test packages and never publishes them. A tagged
release uses the `release` environment and requires signing. It never falls back
to unsigned publication when a certificate is missing.

An unsigned early release is a separate, explicit maintainer choice through the
manual Release workflow with `allowUnsigned: true`. Package validation, privacy
checks and release-asset integrity checks still apply. Release notes must clearly
state that the packages are unsigned and list material validation gaps.

The 0.1.6 early release uses this unsigned path. Two-device provider testing remains
outstanding; it is not reported as passed. Configure release-environment protection
rules when establishing the signed distribution process.

## Signing configuration

Supply `SIGNING_CERTIFICATE_BASE64` and `SIGNING_CERTIFICATE_PASSWORD` as release
environment secrets containing a trusted code-signing PFX and its password.
The workflow imports it into the runner's current-user certificate store for the
packaging operation and removes the imported certificate and temporary PFX afterward.
For local signed packaging, `SIGNING_CERTIFICATE_THUMBPRINT` can instead identify
an existing current-user certificate with an accessible private key.

Velopack performs signing during packaging so its generated setup and update
executables are covered. A timestamp service is used. The script verifies signatures
before generating the final public asset list. A trusted signing certificate must
be provisioned separately; the repository does not contain one.

## Practical validation

- Check the main screen, resizing, long names, keyboard focus and tray behavior.
- Check enabled/disabled button hover, active time-format selection and text-only
  footer link feedback in dark, light and system appearances.
- Compare the application icon in the taskbar for installed and portable copies.
- Check the folder setup guide and local/folder/WoW statuses with no folder, paused
  exchange, unchanged data and read/write errors.
- Check initial installation, uninstallation, autostart and restart after update.
- On two Windows devices, exercise Dropbox and OneDrive with locally resident folders.
- Check offline play, reconnection, delayed and conflicting files, pause and disconnect.
- Verify duplicate characters are counted once and newer server answers correct old estimates.
- Check guild join, leave and pending guild information on every client family, then
  exchange changes between PCs without altering the selected playtime baseline.
- Upgrade a version 1 sync cache and verify that unchanged legacy snapshots remain accepted.
- Remove a character in each app, verify excluded totals and explicit restoration,
  then verify restoration on a fresh login after the addon has received the removal.
- Confirm reloads, zone changes, old files and a running session do not restore it.
- Test simultaneous offline removals/restorations and copied local data, plus
  source deselection and switching to a different group without unrelated controls.
- Upgrade an existing 0.1.5 installation, preserving settings and measurements.
- Verify CurseForge and both GitHub links open the correct default-browser pages.
- Check mouse and keyboard selection in both character lists, including sorting
  and moving focus to an action without losing the full-row underline.
- Confirm a compatible Hourstone 0.2.2+ package is publicly downloadable. Prefer
  CurseForge; when moderation is pending, explicitly link the compatible addon
  GitHub release in the setup instructions and release notes.
- Exercise Retail, Mists Classic, TBC Anniversary and Classic Era with selected accounts.
- Confirm the first data-addon installation requires a WoW restart and subsequent data
  refreshes load on login/reload without modifying SavedVariables.
- Test an update from the previous signed release, both with WoW running and closed.
- Verify the release notes accurately distinguish automated checks from practical checks.

The checklist tracks coverage; it does not imply that each item has passed.
Record completed checks and material gaps in the release notes. Early releases
may document incomplete practical coverage without claiming it was verified.

## Publishing

Commit reviewed public files and release notes before publishing. For a signed
release, push the matching version tag or run the manual Release workflow with
signing enabled. For an explicitly unsigned early release, run the manual workflow
on the reviewed branch, set `tag` to the declared version and `allowUnsigned` to
`true`. Do not push a tag to request an unsigned release: tag-triggered runs still
require a certificate.

The workflow builds fresh packages, so their embedded release notes match the
published version. It uploads exactly five manifest-listed files:

- `HourstoneCompanion-win-Setup.exe` — Windows installer.
- `HourstoneCompanion-win-Portable.zip` — fully extract before use.
- `HourstoneCompanion-<version>-full.nupkg` — automatic update package.
- `releases.win.json` — update feed.
- `SHA256SUMS` — SHA-256 checksums for the four files above.

Start the release notes with direct installer and portable download links for the
matching version; provide German and English labels. GitHub adds two source-code
archives separately. The application and automatic updates use the same package
files regardless of these presentation links.

License, artwork and dependency notices, the dependency inventory (SBOM), and asset
and icon manifests remain inside the application packages. They are not uploaded
again as individual release downloads. `release-manifest.json` remains validation
evidence in the workflow artifacts; it is not a public release asset. Velopack's
legacy `RELEASES` file and build-only `assets.win.json` are not published. Hourstone
uses the modern `releases.win.json` feed and does not migrate Squirrel clients.
Bundled artwork and file integrity are validated before publishing.

A missing tag is created at the validated commit only after checks succeed. An
existing tag must match that commit. An empty release draft can be completed;
published releases and nonempty drafts are not overwritten. Assets are uploaded
and checked before the draft is made public. Inspect an interrupted nonempty draft
before retrying rather than silently replacing its assets.
