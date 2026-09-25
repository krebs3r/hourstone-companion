# Optional Windows App Certification Kit preparation

Prepared on 2026-09-18 (Europe/Berlin). WACK is downloaded but **not installed or run**.

Microsoft now marks WACK as deprecated and unmaintained. Local WACK checks are optional, not a condition of Store submission; official certification runs when a package is submitted to Partner Center. This document preserves the prepared download and an optional installation path, not an outstanding release requirement. [Microsoft's local-validation note](https://learn.microsoft.com/en-us/windows/msix/package/packaging-uwp-apps#validate-your-app-package-locally).

## Download and verification

- Microsoft Windows SDK release: **10.0.26100.9169**, matching the existing SDK build tools.
- Official source: [Windows SDK downloads](https://learn.microsoft.com/en-us/windows/apps/windows-sdk/downloads).
- Official installer link: <https://go.microsoft.com/fwlink/?linkid=2376216>.
- Downloaded installer: `artifacts/wack-tools/winsdksetup-10.0.26100.9169.exe`.
- Installer size: **1,449,536 bytes**.
- Installer SHA-256: `680AA29DCFA806D35B4E93EA05A3FA2BDCF2935B65295F996C8E944584DD2836`.
- Authenticode status: **Valid**, signer **Microsoft Corporation**, issuer **Microsoft Windows Code Signing PCA 2024**.
- Signer thumbprint: `B835FC295FFB94EA2FCA23B0E9C1EDA3FBA4E07D`.
- The installer's internal product version is `10.1.26100.9169`; Microsoft labels the SDK release `10.0.26100.9169`.

`artifacts/wack-tools/installer-provenance.json` records the resolved Microsoft download URL, signature information, hash and download time.

## Smallest supported feature selection

The signed installer's embedded `UserExperienceManifest.xml` identifies:

- `OptionId.WindowsSoftwareLogoToolkit`: Windows App Certification Kit.
- Its dependency: `OptionId.AvrfExternal` (Application Verifier).
- WACK MSI packages are marked `PerMachine="yes"` in the signed bundle manifest.

The bundle supports WACK as a machine-wide installation. No supported per-user WACK installation mode was found. Merely choosing a user-writable install directory would not change the package installation scope.

The SDK bundle also downloads its infrastructure/prerequisites, including the .NET Framework payload and SDK license package, as part of this selected feature layout. It downloads applicable feature payloads for multiple architectures; this is not a complete Windows SDK download.

## Completed download-only operation

The embedded installer help explicitly describes `/layout` as download-only. The embedded wizard definition includes an EULA page for installation but not for the Layout workflow. Its executable manifest uses `asInvoker`.

From the repository directory, the completed command was equivalent to:

```powershell
& .\artifacts\wack-tools\winsdksetup-10.0.26100.9169.exe `
    /layout "$PWD\artifacts\wack-tools\layout" `
    /features OptionId.WindowsSoftwareLogoToolkit `
    /quiet /norestart /ceip off `
    /log "$PWD\artifacts\wack-tools\layout-download.log"
```

Result:

- Exit code **0**; layout contains **40 files, 144,255,228 bytes**.
- All **39 EXE/MSI/CAB files** have **Valid** Authenticode signatures.
- Every payload covered by the signed bundle manifest matches its expected hash; no mismatches.
- `layout-download.log` records `action: Layout`, `WixBundleElevated = 0`, and `restart: None`.
- No machine installation, license acceptance, certificate trust change, or app certification test was performed.

Verification records:

- `artifacts/wack-tools/layout-payload-verification.json`: SHA-256, original manifest hash comparison and signer for every layout file.
- `artifacts/wack-tools/installer-help-resources.json`: help text extracted from the verified installer without launching its UI.
- `artifacts/wack-tools/bootstrapper-metadata/UserExperienceManifest.xml.formatted.xml`: feature/dependency and wizard definitions.
- `artifacts/wack-tools/bootstrapper-metadata/0`: original embedded bundle manifest, including `PerMachine` attributes.

## Optional installation, only if requested

The following command has **not** been run. If this optional check is requested, installation involves reviewing and accepting Microsoft's SDK license and approving its machine-wide installation/UAC prompt:

The exact license resources extracted from the verified installer are available for prior review at `artifacts/wack-tools/bootstrapper-metadata/license.rtf` (Windows SDK license) and `artifacts/wack-tools/bootstrapper-metadata/NetfxLicense.rtf` (the bundled .NET Framework prerequisite). The installer remains the authoritative license-acceptance UI.

```powershell
& .\artifacts\wack-tools\layout\winsdksetup.exe `
    /features OptionId.WindowsSoftwareLogoToolkit /norestart
```

Keep **Windows App Certification Kit** selected; Application Verifier is its declared dependency. Review the installer before proceeding. `/norestart` prevents an automatic restart or restart prompt.

After installation, the usual WACK path is:

```text
C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe
```

Microsoft's [WACK command-line instructions](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit#validate-your-windows-app-using-the-windows-app-certification-kit-from-a-command-line) require an elevated command window in an active user session. Schedule the actual test separately because WACK exercises the app:

```powershell
& 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' reset
& 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' test `
    -appxpackagepath '<absolute path to the signed MSIX>' `
    -reportoutputpath '<absolute output report path>'
```

No certification outcome should be inferred from this preparation. If the optional test is run, retain and review its report. No WACK installation or report is required to start Store certification.
