# Native MSIX validation — 2026-09-18

Current candidate evidence: [24 September certification remediation](store/CERTIFICATION-FIX-2026-09-24.md), including a fresh native LocalTest install/update run with the current DLL. The report below remains the historical 18 September record.

The separate `HourstoneCompanion.LocalTest` package passed native installation, normal-user activation, upgrade, profile preservation, second activation and removal on the existing Windows 11 x64 test host. Both activated WPF processes ran at **Medium Integrity, RID 8192**. The existing direct app, its startup entry, real WoW installation and real cloud folders were not changed by this test.

This is historical evidence for the build **before the visible Blizzard credit**. It is **not** an installation test of the production Partner Center identity or the later credits candidate (`fd68149bce35004a28c7fbc90d1b61988f4d68e08f6e87cdb4fa0a6d7b0f4303`). The tested files were prepared from the earlier LocalTest MSIX and signed together using one short-lived test certificate, because the original package's private signing key had already been deleted. Both test versions contain this earlier application DLL:

`581876d9a407bf8768c1a32aeb37a21da214847e013479463b571a1ad5ce2ebd`

The original LocalTest MSIX remains unchanged, SHA256 `fcbf10cf4cfcd22cfd407668e1c726bc83f83550a45ca97a3697b9cbb55a676d`.

## Exact prepared sequence

All paths below are relative to the repository. The test root is:

`artifacts/msix-validation/prepared/20260917-223115-c9179a4452724dd0ae20a57207d03f05/`

| Evidence | Value |
|---|---|
| Reviewed plan | `test-plan.json`, SHA256 `fac9c27a01e2a763b0f42f05d014dbc97ad7fe95968380da5ee64edd59eccab8` |
| First package | `HourstoneCompanion.LocalTest-1.0.0.0-x64.msix`, SHA256 `edc9b5eed73032421b39daf7e764bc393d0cdca504f01db3fe917cc148f6ced4` |
| Update package | `HourstoneCompanion.LocalTest-1.0.1.0-x64.msix`, SHA256 `80f9f91d169e23848e544e974daa78869be3a81dd41ee1dead0f607d0f1b9a7e` |
| Public test certificate | `Validation.cer`, thumbprint `E6A2B4C548ADDC30617C96DFF0506BA6ADAD1371` |
| Certificate scope | `CN=HourstoneCompanion.LocalTest`, code-signing EKU only, DigitalSignature, not a CA, no retained private key |
| Certificate validity | 2026-09-17 22:26:16 UTC to 2026-09-19 22:31:16 UTC |
| Successful native result | `results/20260917-224052-02886e358feb4564a2d3331144d5b132/native-test-result.json` |
| Initial/updated reports | `initial-startup.json` and `updated-startup.json` in that result directory |
| Profile backup/evidence | `profile-backup/` and `profile-before-update.sha256` in that result directory |
| Independent trust cleanup | `trust-lease-20260918-medium-native/trust-lease.json`: `active=false`, `removed=true`, `released=true`, no error |

The test installs 1.0.0.0 for the current Windows user and activates its actual AUMID through `IApplicationActivationManager`. `--smoke-test` uses a new `LocalState/validation-smoke` directory inside the package's private profile. The normal WPF window, dispatcher, SQLite initialization and an empty local sync must succeed. A synthetic marker and all profile file hashes are preserved across the native 1.0.1.0 update before the second activation. The package is then removed.

The native runner does not import real settings, add WoW/cloud sources or alter autostart. It snapshots the existing direct edition's `HourstoneCompanion` Run value and verifies it is unchanged. The result includes a synthetic profile backup, not any existing user profile.

## Trust boundary and cleanup

The user expressly approved temporary trust for exactly the certificate above in **LocalMachine/TrustedPeople**. Microsoft specifies this store for full signed test MSIX packages; it is not the Trusted Root store. See [Microsoft's certificate guidance](https://learn.microsoft.com/en-us/windows/msix/package/create-certificate-package-signing).

The actual app test must run from a normal, unelevated Windows token. An initial administrative runner activated the app at High Integrity (RID 12288); the integrity guard rejected that run and its package/certificate were removed. A separate attempt inside the execution sandbox could not open the package and was also cleaned up. Neither attempt is counted as a passed native run.

The successful run separates duties:

1. `tools/with-msix-test-trust.ps1` receives Windows UAC approval for the original user SID. It validates the pinned plan, imports only its certificate, starts no application, and holds trust for at most five minutes.
2. `tools/test-msix-native.ps1` runs outside the file sandbox with a **normal Windows token**. It verifies hashes, certificate purpose/validity, manifest identity and signed package trust, then performs the native test.
3. The normal runner signals the lease's `release` file in a `finally` block. The elevated helper independently removes its certificate in its own `finally` block. It also expires automatically if the normal runner does not signal within five minutes.
4. Fresh independent reads confirmed no installed LocalTest package, no LocalTest profile directory, no approved certificate in TrustedPeople, and no changes to the previous direct startup value. No Windows security policy, Smart App Control rule, Defender setting or Code Integrity exception was changed.

`native-test-result.json` and the independent trust-lease report must be considered together. The normal runner sees the certificate as already trusted while the helper owns its lifetime; its own certificate flag alone is not proof that the helper has removed trust.

## Reproduction and remaining coverage

`tools/prepare-msix-validation.ps1` prepares a new pair and reviewable plan without installation or trust. The public-only certificate expires after two days; regenerate and review the new certificate/plan when needed. `tools/test-msix-native.ps1 -PlanPath <plan>` defaults to preflight only. Actual execution additionally requires the reviewed plan SHA256, the original Windows user SID and an explicitly authorized temporary trust step. Never disable Windows protection to make a test pass.

This result does not cover the normal first-run wizard, guided transfer/rollback, actual removal of legacy autostart during a handover, StartupTask states, reboot/sign-in, tray interactions, simultaneous real-channel instances, writes to a real WoW installation/cloud provider or Store certification. Those remain separate items in the [submission checklist](store/submission-checklist.md). WACK was not run; it is a deprecated optional local check, not a submission prerequisite. Its prepared offline payload is documented in [WACK preparation](WACK-PREPARATION.md).
