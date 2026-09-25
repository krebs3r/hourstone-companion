# Development

The solution contains a WPF application, a platform-independent synchronization
core and automated tests. Use Windows 11 x64 and the .NET SDK pinned by `global.json`.
The current product version is **0.2.0**, including the earlier 0.1.7 fixes.
GitHub releases use the direct installer/portable channel; Store certification
is tracked separately in [STORE.md](STORE.md).

```powershell
python tools/verify_assets.py
python tools/check_protocol.py
dotnet restore Hourstone.Companion.slnx --locked-mode
dotnet build Hourstone.Companion.slnx -c Release --no-restore
dotnet test Hourstone.Companion.slnx -c Release --no-build
```

Dependency upgrades are intentional changes: update the pinned versions, refresh
all affected `packages.lock.json` files, then run tests and the Windows render checks.
The release tool is pinned in `.config/dotnet-tools.json`.

## Bundled assets

Run `python tools/verify_assets.py` before building or packaging. The offline,
standard-library-only check verifies the complete catalog of 13 class icons, four
client icons, the heart and the unknown marker against `docs/assets-manifest.json`.
It checks SHA-256, PNG chunk checksums, decoded image data and dimensions, and rejects
missing, extra or changed catalog files. CI and the package script both run this gate.
The Windows ICO is also verified against `docs/app-icon.json`, including all nine
resolutions and its unchanged logo source. To regenerate it, install Pillow 12.3.0
and run `python tools/build_app_icon.py`; Pillow is not needed for normal builds.
Intentional artwork changes must update the asset manifests and `docs/ASSETS.md`.

## UI checks

`--demo` uses synthetic characters. `--render` produces a deterministic preview
without selecting real WoW data or enabling normal background synchronization:

```powershell
pwsh -File tools/render-smoke.ps1 -Extended
```

The script renders dark and light themes at 100%, 150% and 200% scaling and validates
PNG dimensions. Selected rows and the footer have layout assertions; empty, missing
and outdated source profiles verify the acquisition actions. Synchronization
profiles cover disconnected, connected, paused and failed exchange, and unchanged
data. Ordinary button states and selected/inactive time formats are checked separately. Settings, compact layouts and the full icon catalog also have
synthetic render profiles with guilds, guildless characters and missing guild information. These checks demonstrate rendering and output integrity; visual
review of layout, text truncation and keyboard use is still required.

`--diagnostics-state cloud-file-not-local` renders the cloud availability banner
and Synchronization diagnostics with a synthetic file path. Extended render checks include
German and English examples in normal and compact windows.
`--caption-focus minimize|maximize|close` checks that ordinary focus leaves no
outline inside a title-bar button; keyboard navigation retains its separate focus
visual. Extended checks cover all three buttons in dark and light appearance.

Progress profiles cover the Retail table and character details, nine Vault slots,
independent timestamps, unknown and stale values, English/German, compact windows
and 100–200% scaling. Store profiles exercise the first-run transfer states and
Store-specific settings. These use synthetic data and do not prove a native WoW
session, an installed MSIX handover or transport through a real cloud provider.

## Local package

Packaging requires PowerShell 7.4 or newer and Python 3.12 or newer.

```powershell
pwsh -File tools/package.ps1 -Unsigned
```

The package is a self-contained win-x64 publish built with locked dependencies.
The script creates an installer, update packages, checksums and dependency notices.
It also starts the fully extracted portable root launcher using an isolated test
profile. `tools/startup-smoke.ps1` checks the normal WPF window, dispatcher, completed
local sync and database creation; it refuses an existing profile as its target.
Artwork notices and assets-manifest.json are included in packages and release assets.
Regular validation CI keeps unsigned output private. Publishing an unsigned early
release requires the explicit manual option documented in [Releasing](RELEASING.md);
a missing signing certificate never causes an automatic fallback.

The direct installer and portable build remain usable independently of Store
setup and retain their Velopack/GitHub update channel. A separate
`tools/package-msix.ps1` prepares a Store MSIX with either a local test identity or
explicit production identity. Store copies use Store updates and a separate local
profile; they do not initialize Velopack. See [Store preparation](STORE.md) for the
first-run transfer, autostart handover, signing and installed-package checks.

Artifacts under `artifacts/releases`, `artifacts/msix` and `artifacts/addon` are
local outputs, not evidence of publication. Inspect their manifests, package
versions and validation records before distributing them; a previous output can
still describe 0.1.7 until that channel has been rebuilt. Addon 0.3.2 is packaged
separately as `artifacts/addon/Hourstone-0.3.2.zip` with its SHA-256 sidecar.

All generated files live under `artifacts/`. Do not store real account data,
provider snapshots, credentials or planning material in this repository. Use only
synthetic fixtures for tests. Install the local pre-push guard with
`python tools/install_hooks.py`.

## Contract ownership

The addon and Companion working trees share [protocol version 4](sync-protocol-v4.md)
and retain legacy protocols 1–3. The documents and synthetic fixtures are mirrored
with SHA-256 pins in `docs/protocol-pin.json`. Run `python tools/check_protocol.py`
to verify all pinned content offline. The retained `sourceCommit` identifies the
legacy 1–3 pin only. `protocol4SourceCommit` identifies the published addon 0.3.2
commit; its v4 document and two synthetic fixtures were verified byte-for-byte
against the vendored files before the Companion 0.2.0 release. Protocol changes must update both repositories and their
conformance tests in one compatible release.

Companion 0.2.0 reads local Retail progress saved by addon 0.3.1 or later.
Cloud snapshots use format 4, so every participating Companion must be upgraded
to 0.2.0 together. Generated addon output uses format 4 only when the installed TOC
advertises exactly `## X-Hourstone-Sync-Protocol: 4`, supplied by addon 0.3.2.
Without that capability, output stays at format 3 and carries playtime, guild and
visibility data. Local schema-3 SavedVariables and progress-cache version 1 remain
unchanged; the Companion database migrates atomically to schema 2.

The five progress families use independent, deterministic whole-record merges.
Raw storage and transport preserve source provenance; combined display values are
terminal projections. Imported progress never becomes a local contribution, and
unknown future progress caches do not block playtime. The shared v4 fixture tests
and the actual C# writer-to-Lua import check cover this boundary. The addon suite
passes 90 Lua scenarios and 38 Python tests; native 0.3.2 and practical two-PC cloud
acceptance remain separate work.
