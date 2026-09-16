# Development

The solution contains a WPF application, a platform-independent synchronization
core and automated tests. Use Windows 11 x64 and the .NET SDK pinned by `global.json`.

```powershell
dotnet restore Hourstone.Companion.slnx --locked-mode
dotnet build Hourstone.Companion.slnx -c Release --no-restore
dotnet test Hourstone.Companion.slnx -c Release --no-build
```

Dependency upgrades are intentional changes: update the pinned versions, refresh
all affected `packages.lock.json` files, then run tests and the Windows render checks.
The release tool is pinned in `.config/dotnet-tools.json`.

## UI checks

`--demo` uses synthetic characters. `--render` produces a deterministic preview
without selecting real WoW data or enabling normal background synchronization:

```powershell
pwsh -File tools/render-smoke.ps1
```

The script renders dark and light themes at 100%, 150% and 200% scaling and validates
PNG dimensions. These checks demonstrate rendering and output integrity; visual
review of layout, text truncation and keyboard use is still required.

## Local package

Packaging requires PowerShell 7.4 or newer and Python 3.12 or newer.

```powershell
pwsh -File tools/package.ps1 -Unsigned
```

The package is a self-contained win-x64 publish built with locked dependencies.
The script creates an installer, update packages, checksums and dependency notices.
Unsigned output belongs to local tests and must not be uploaded as a public release.

All generated files live under `artifacts/`. Do not store real account data,
provider snapshots, credentials or planning material in this repository. Use only
synthetic fixtures for tests. Install the local pre-push guard with
`python tools/install_hooks.py`.

## Contract ownership

The addon repository owns protocol version 1. Its document and synthetic fixtures
are mirrored with SHA-256 pins in `docs/protocol-pin.json`. Run
`python tools/check_protocol.py` to verify the pinned content. Protocol changes
must update both repositories and their conformance tests in one compatible release.
