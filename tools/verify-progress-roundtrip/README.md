# Reproducible simulated progress roundtrip

Run from the Companion repository in PowerShell:

```powershell
./tools/verify-progress-roundtrip.ps1 -OutputDirectory ./artifacts/roundtrip-final
```

The output directory must be new and inside this repository's `artifacts` directory.
Choose another name for a repeat run; existing evidence is never deleted or reused.
Optional `-AddonRoot`, `-Python` and `-Dotnet` parameters support other local layouts.
Requirements: .NET 10 SDK and restored Companion dependencies; the Hourstone addon
source repository, including its local `v0.3.1` tag; Python with `lupa.lua51` and
Pillow (the addon test environment). Dependency vulnerability auditing is skipped
only for this isolated verification harness; production auditing remains separate.

The script creates two synthetic profile databases, two synthetic WoW roots and two
separate cloud directories. It copies publication files between those directories
to simulate transport. `CompanionService` reads the SavedVariables, stores local and
remote observations and writes real data addons. Python executes those exact Lua
files using the addon modules and a Lua 5.1 runtime. The legacy case loads the actual
addon source from tag `v0.3.1`; the remaining cases use the current addon working tree.
No real profile, WoW installation or cloud folder is selected.

The ten checkpoints cover legacy protocol 3, protocol 4 in both directions, independent
winners for all five progress families, a newer lower key, confirmed no key, stale
data after a weekly boundary, zero values in the new week, a damaged peer snapshot,
a future optional local cache version and an absent local cache. Assertions also
cover stable revisions, independent playtime selection, unchanged SavedVariables,
preserved peer revisions and absence of foreign data in local addon caches or local
publications. Every checkpoint contains the actual `Data.lua`, SavedVariables and
own publication. `report.json` contains results and SHA256 hashes of generated files.

This is a local integration simulation. It does not establish native WoW API behavior,
real weekly resets, cloud-provider synchronization, Store installation or operation
on two physical PCs. Use the existing native/two-PC acceptance checklist for those.
