# Store remediation checklist — 24 September 2026

Product: Hourstone Companion, Store ID `9N9J1P7PKQTJ`, product 0.2.0, candidate MSIX 1.0.1.0.

The 1.0.0.0 submission failed certification on 21 September under 10.6.3. The capability name is a placeholder in the report. The original checklist, package and portal evidence are preserved under `artifacts/archive/20260924-before-certification-fix/`.

- [x] Remove `unvirtualizedResources` and the registry exclusion; retain `runFullTrust` and optional StartupTask.
- [x] Replace legacy-autostart mutation with manual handover guidance and confirmation.
- [x] Preserve staged profile import, backup, new device ID and single-writer gate.
- [x] Credits: Consolas body, 24-point heading and explicit Blizzard line breaks.
- [x] Move Local diagnostics to Synchronization; update German/English notices and previews.
- [x] Record final build, automated tests, renders and package hashes in [the remediation record](CERTIFICATION-FIX-2026-09-24.md).
- [x] Native LocalTest install, activation, update and cleanup with the candidate DLL; running-legacy-app gate confirmed.
- [ ] Remaining installed wizard, tray, Windows sign-in startup and external-output checks; see the remediation record. The user deprioritized full profile transfer testing because source character data can be re-read.
- [x] Verify GitHub website and issues without authentication.
- [x] Save and reopen the Partner Center support contact `mail@martin-krebs.eu`, retaining the GitHub website.
- [x] Upload package 1.0.1.0, fourteen updated screenshots, privacy text and reviewer notes; saved and verified on 25 September.
- [x] Resubmit for certification as explicitly requested: 25 September 2026, 19:36 UTC. Portal: In certification; pre-processing in progress. Approval/publication pending.

Do not mark certification, artwork clearance, real cloud-provider exchange, or native WoW testing passed based on this checklist. See [asset-review.md](asset-review.md) for the previously documented artwork decision.
