# Release validation

Public releases use `v<version>` tags in this repository. The application version
in `Directory.Build.props` must match the tag. Updates are sourced exclusively
from `krebs3r/hourstone-companion`; addon releases have their own version series.

## Automated checks

- Offline bundled-artwork verification with `python tools/verify_assets.py`: catalog,
  SHA-256, PNG structure and dimensions against `docs/assets-manifest.json`.
- Locked solution restore, Release build and core/conformance tests.
- Dark/light render smoke checks at 100%, 150% and 200%; validation CI also renders
  system theme, settings, all classes, long names and compact layouts.
- Repository privacy guard across the current tree and newly introduced commit blobs.
- Self-contained win-x64 packaging, package content allowlist and checksums.
- Valid Authenticode signatures for the installer and executable payloads.

A normal CI run creates unsigned test packages and never publishes them. A tagged
release uses the `release` environment and fails if signing is unavailable.
It never falls back to unsigned publication.

Configure repository environment protection rules for release maintainers before publishing.

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

## Practical checks before a public tag

- Check the main screen, resizing, long names, keyboard focus and tray behavior.
- Check initial installation, uninstallation, autostart and restart after update.
- On two Windows devices, exercise Dropbox and OneDrive with locally resident folders.
- Check offline play, reconnection, delayed and conflicting files, pause and disconnect.
- Verify duplicate characters are counted once and newer server answers correct old estimates.
- Check guild join, leave and pending guild information on every client family, then
  exchange changes between PCs without altering the selected playtime baseline.
- Upgrade a version 1 sync cache and verify that unchanged legacy snapshots remain accepted.
- Exercise Retail, Mists Classic, TBC Anniversary and Classic Era with selected accounts.
- Confirm the first data-addon installation requires a WoW restart and subsequent data
  refreshes load on login/reload without modifying SavedVariables.
- Test an update from the previous signed release, both with WoW running and closed.
- Verify the release notes accurately distinguish automated checks from practical checks.

No successful live-client, two-device or public-signing validation is implied by
the presence of these checklists. Record completed validation in the release notes.

## Publishing

After checks pass, update the version and release notes, commit reviewed public
files and push the matching version tag. The release workflow uploads only files
listed in the generated release manifest, including `ASSET-NOTICES.txt` and
`assets-manifest.json` with their checksums. Bundled-artwork verification runs again
at the start of packaging, including signed releases. Package signing and validation happen
before a GitHub release is created.
