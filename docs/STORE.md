# Microsoft Store preparation

Resubmitted package (25 September 2026): package **1.0.1.0**, product **0.2.0**. Certification of 1.0.0.0 failed on 21 September under **10.6.3 Capabilities**. The report leaves the capability name as a placeholder; `unvirtualizedResources` is the likely cause. The new candidate removes it and uses manual legacy-startup handover. Support: **mail@martin-krebs.eu**, with GitHub retained as the project website. See [the remediation record](store/CERTIFICATION-FIX-2026-09-24.md). Resubmitted at 19:36 UTC / 21:36 Europe/Berlin; Partner Center confirmed In certification, with pre-processing in progress. Approval and publication are pending.

The direct Windows app remains independently runnable and uses its existing Velopack/GitHub release pipeline. Neither its build nor its first launch requires a Microsoft Store identity, account, approval, or signing certificate when explicitly building locally with `tools/package.ps1 -Unsigned`.

The additional Store artifact is a self-contained Windows 11 x64 MSIX containing the existing WPF application. `runFullTrust` permits the normal desktop process; the package does not request elevation or add Windows services. Local test and production package identities are deliberately separate. Packaging never publishes a release, submits to Partner Center, installs the app, or trusts a certificate.

## Generate a local test package

```powershell
pwsh -File tools/package-msix.ps1 -Profile LocalTest -SignLocalTest
```

The script restores the pinned Microsoft Windows SDK BuildTools NuGet package under `artifacts/tool-packages`. A full system SDK installation is not required to generate the package. `-SdkBin` can select an existing SDK's x64 tool directory instead. The application and tool dependencies use separate lockfiles.

Output is under `artifacts/msix/LocalTest`: an MSIX, checksum, validation record and optional public `LocalTest.cer`. `-SignLocalTest` creates a short-lived local certificate without adding it to a certificate store; its temporary private PFX is removed. The generated signature is for controlled testing, not public trust or production submission. Installing/trusting this test package is a separate explicit testing step. Without that switch, MakeAppx produces an unsigned test package.

## Prepare the production identity

Reserve the actual product in Partner Center and populate a private/local copy of `packaging/store-identity.example.json` from its Product identity page. All fields are required. The production package Name and Publisher must exactly match Partner Center. The script rejects blank/example/local-test identity values before publishing any application files.

```powershell
pwsh -File tools/package-msix.ps1 -Profile Store -IdentityPath <actual-identity.json>
```

The initial package version was `1.0.0.0`; the remediation candidate is `1.0.1.0`. This is independent of the companion and WoW addon's product versions. Keep Store package versions increasing, with a nonzero first field and zero fourth field. Production MSIX output does not require a purchased certificate: Microsoft re-signs approved Store packages. No production identity has been invented in this repository.

The manually dispatched `Prepare Microsoft Store package` workflow supports both profiles. Production values come from the `STORE_PACKAGE_NAME`, `STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY_NAME`, `STORE_PRODUCT_ID`, and `STORE_PACKAGE_VERSION` repository variables. It uploads reviewable artifacts only and has no submission credentials or publish operation.

Historical submission record (2026-09-18; superseded by the failure on 2026-09-21): **Hourstone Companion**, Store ID `9N9J1P7PKQTJ`, package name `MartinKrebsSoftware.HourstoneCompanion`, Publisher `CN=14D320E3-ECEA-427C-B053-C559BFCB7342`, display name **Martin Krebs Software**, package version `1.0.0.0`. That credits package was **submitted for certification at 15:01 UTC (17:01 Europe/Berlin)** under submission `1152921505701922590`. All six sections were Complete and the package Validated before submission. At that time Partner Center showed **In certification**, with **Submission Complete**, **Pre-processing In progress**, **Certification Not started**, **Publishing Not started**. Automatic publication after successful certification is confirmed; the product is not yet published.

The historical submission used the full bilingual **Privacy policy text** option, so a separate hosted privacy URL is not a blocker. The 25 September resubmission contains seven updated synthetic screenshots per language, including the handover, revised Credits and Local diagnostics under Synchronization. Submitted pricing is free (Germany/EUR reference), publicly discoverable, with the portal's unchanged worldwide default of 240 markets and future markets selected. The saved IARC rating ID is **Pending**. The historical notes contained 4,886 characters. The replacement 4,820-character notes were saved and reopened before the 25 September resubmission; the exact text is retained in `artifacts/store-submission/reviewer-notes-submitted-20260925.txt`. Microsoft reviews restricted capabilities during certification. The publisher explicitly chose to proceed with the unchanged Blizzard assets despite undocumented rights. See [the checklist](store/submission-checklist.md) and local `artifacts/store-submission/partner-center-status.json` for the record and remaining functional-test limits.

## Runtime and transfer

Package identity is detected before initializing Velopack. Store copies do not initialize the GitHub updater; their update action opens Microsoft Store. Direct installed and fully extracted portable copies keep the existing GitHub feed. Development builds do not pretend to support installed updates.

Store preferences and SQLite live in the package's LocalFolder. Direct copies keep `%LocalAppData%\Hourstone\Companion`. First Store launch offers a controlled transfer before synchronization or file watchers start. Transfer uses SQLite's backup API, validates a private staging copy and assigns a fresh device ID and revision. Source IDs, visibility and cached observations are preserved. A complete source backup is retained under the Store profile's `import-backups`; source profile data is unchanged. A completed destination is never silently overwritten or imported twice.

The old cloud snapshot can remain as a historical device contribution. Character merging prevents duplicate playtime, but an old source can remain visible through that historical snapshot after it is locally deselected. No other PC's cached state or cloud file is automatically deleted.

Only one active copy should write WoW and shared-folder data. A per-user cross-session mutex and the legacy session mutex coordinate new distribution channels and older releases in the same session. A running old copy blocks activation of Store synchronization; the setup shell remains visible. The user disables the old autostart in Windows startup settings and confirms this before switching. The Store app never changes the old Run value. Store startup follows the user's choice, with rollback of its own StartupTask on failure. Older binaries do not understand newer handover state; users should stop using or uninstall the old installed copy after switching, and avoid starting an old portable copy in another Windows session.

Store autostart uses a manifest StartupTask and respects Windows' user/policy disabled states. The task starts hidden; normal Store launch opens the window. The direct installed copy continues using its own HKCU Run entry; portable builds do not install autostart.

The manifest requests only `runFullTrust`. It contains no registry virtualization exclusions. The old Run value is read only to decide whether to show handover guidance. A remaining value does not block setup because Windows can disable startup without deleting it. Missing read access also shows the guidance. The original profile is copied, not modified; the Store edition uses its own StartupTask.

## Validation evidence and remaining functional checks

The [24 September remediation record](store/CERTIFICATION-FIX-2026-09-24.md) binds the current package hash to 341 .NET tests, 40 Python tests, 125 extended renders, direct-package checks and native LocalTest installation/update/removal. The installed setup gate was also checked while the real direct app remained running. The [18 September native report](MSIX-NATIVE-VALIDATION.md) remains historical evidence.

`package-validation.json` is a build-time snapshot for the current production candidate, which was uploaded and resubmitted on 25 September but has not been installed locally under the production identity. Its build-time submission flag remains historical; current portal status is in `artifacts/store-submission/partner-center-status.json`. The current LocalTest package contains the same application DLL. Remaining native checks include the full setup/transfer flow, tray operation after setup, actual Windows sign-in startup and external file visibility from a normally configured Store profile. Mocked StartupTask tests and the isolated native smoke test do not establish those results. See the remediation record for exact scope and the user's reduced priority for complete data-transfer testing.

WACK is deprecated, unmaintained and optional; its [prepared download](WACK-PREPARATION.md) has not been installed or run. It is not a submission prerequisite. Microsoft performs official certification after submission, including [restricted-capability review](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations#restricted-capability-approval-process). See [Microsoft's local-validation note](https://learn.microsoft.com/en-us/windows/msix/package/packaging-uwp-apps#validate-your-app-package-locally).

For an isolated real direct-app launch, use the actual installed/portable launcher with:

```powershell
& '.\Hourstone Companion.exe' --smoke-test --data-directory <isolated-test-directory>
```

The flag opens the normal application with a separate data folder and instance gate, suppresses autostart/update/import side effects, writes `smoke-result.json`, and exits. It is distinct from synthetic render validation and does not use existing user preferences or sources.

Official references: [manual MSIX components](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion), [Store package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements), [desktop startup activation](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/get-activation-info-for-packaged-apps), [targeted registry virtualization exclusion](https://learn.microsoft.com/en-us/windows/msix/desktop/flexible-virtualization), [SQLite backup](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup), [pinned Microsoft SDK tools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.26100.9169).
