# Development

The solution contains a WPF application, a platform-independent synchronization
core and automated tests. Use Windows 11 x64 and the .NET SDK pinned by `global.json`.

```powershell
python tools/verify_assets.py
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

## Local package

Packaging requires PowerShell 7.4 or newer and Python 3.12 or newer.

```powershell
pwsh -File tools/package.ps1 -Unsigned
```

The package is a self-contained win-x64 publish built with locked dependencies.
The script creates an installer, update packages, checksums and dependency notices.
Artwork notices and assets-manifest.json are included in packages and release assets.
Regular validation CI keeps unsigned output private. Publishing an unsigned early
release requires the explicit manual option documented in [Releasing](RELEASING.md);
a missing signing certificate never causes an automatic fallback.

All generated files live under `artifacts/`. Do not store real account data,
provider snapshots, credentials or planning material in this repository. Use only
synthetic fixtures for tests. Install the local pre-push guard with
`python tools/install_hooks.py`.

## Contract ownership

The addon repository owns protocol version 3 and retained legacy protocols 1 and 2. Its document and synthetic fixtures
are mirrored with SHA-256 pins in `docs/protocol-pin.json`. Run
`python tools/check_protocol.py` to verify the pinned content. Protocol changes
must update both repositories and their conformance tests in one compatible release.
