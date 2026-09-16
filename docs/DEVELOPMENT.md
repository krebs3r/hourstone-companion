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
Intentional artwork changes must update the asset manifest and `docs/ASSETS.md`.

## UI checks

`--demo` uses synthetic characters. `--render` produces a deterministic preview
without selecting real WoW data or enabling normal background synchronization:

```powershell
pwsh -File tools/render-smoke.ps1 -Extended
```

The script renders dark and light themes at 100%, 150% and 200% scaling and validates
PNG dimensions. Settings, compact layouts and the full icon catalog also have
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
Unsigned output belongs to local tests and must not be uploaded as a public release.

All generated files live under `artifacts/`. Do not store real account data,
provider snapshots, credentials or planning material in this repository. Use only
synthetic fixtures for tests. Install the local pre-push guard with
`python tools/install_hooks.py`.

## Contract ownership

The addon repository owns protocol version 2 and retained legacy protocol 1. Its document and synthetic fixtures
are mirrored with SHA-256 pins in `docs/protocol-pin.json`. Run
`python tools/check_protocol.py` to verify the pinned content. Protocol changes
must update both repositories and their conformance tests in one compatible release.
