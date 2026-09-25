# Hourstone Companion 0.2.0

Windows 11 x64 · .NET enthalten / .NET included

- [Windows-Installer herunterladen / Download Windows installer](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.0/HourstoneCompanion-win-Setup.exe)
- [Portable-Ausgabe herunterladen / Download portable edition](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.0/HourstoneCompanion-win-Portable.zip)
- [SHA-256-Prüfsummen / SHA-256 checksums](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.0/SHA256SUMS)

## Deutsch

**Vor dem Update:** Alle Companions einer Synchronisierungsgruppe gemeinsam auf
**0.2.0** aktualisieren. Neue Cloud-Snapshots verwenden Protokoll 4 und können von
älteren Companions nicht gelesen werden. Bestehende Snapshots bleiben lesbar;
Einstellungen, Spielzeit, Sichtbarkeit und Sync-Gruppe bleiben erhalten.
Die lokale Datenbank wird auf Schema 2 migriert; ältere Companions können dieses
Profil anschließend nicht mehr öffnen.

### Neu und verbessert

- Retail-Fortschritt mit Schlüsselstein, Wochenbestwert und allen neun Plätzen der
  Großen Schatzkammer. Charakterdetails zeigen Quelle, Schwierigkeit und Zeitpunkt;
  unbekannte, bestätigte leere und veraltete Werte bleiben unterscheidbar.
- Fortschritt zwischen eigenen PCs und zurück ins Addon synchronisieren. Jede
  Fortschrittsfamilie behält ihre eigene Quelle und Zeit; empfangene Werte werden
  nicht erneut als lokale Messungen veröffentlicht.
- Verständliche Hinweise für noch nicht lokal verfügbare Cloud-Dateien, auch für
  Proton Drive. Sobald Dateien lesbar sind, werden sie erneut geprüft. Gültige
  gespeicherte Daten bleiben bei Lesefehlern erhalten.
- Lokale Diagnose unter **Synchronisierung**, verbesserte Credits und korrigierter
  Mausfokus der Titelleisten-Schaltflächen.
- Vorbereitung der Store-Ausgabe mit eigenem Profil, Store-Updates, optionaler
  Datenübernahme und Anleitung zum manuellen Abschalten des bisherigen Autostarts.
  Die Direktversion nutzt weiterhin GitHub-Updates.

**Addon:** Spielzeit, Gilden und Sichtbarkeit benötigen 0.2.2 oder neuer; lokale
Retail-Fortschrittsdaten 0.3.1 oder neuer. Der Rückweg des Fortschritts ins Spiel
benötigt mindestens 0.3.2 mit Protokoll-4-Kennung.
[Addon 0.3.3 herunterladen](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.3).

**Unsignierte GitHub-Pakete:** Windows kann einen unbekannten Herausgeber anzeigen
oder die Installation blockieren. Beim früheren lokalen Setup-Test wurde der
Installer von Windows Code Integrity blockiert. Die Portable-ZIP vollständig
entpacken und über den enthaltenen Rootlauncher starten.

**Store-Status:** Paket 1.0.1.0 (Produkt 0.2.0) wurde am 25. September 2026 erneut
zur Zertifizierung eingereicht. Die Freigabe steht aus. Dieses GitHub-Release
bedeutet keine Store-Veröffentlichung.

**Validierung:** 341 .NET-Tests und 40 Python-Prüfungen bestanden. Die vorhandene
Paketabnahme umfasst 125 erweiterte Renderfälle, Paket-/Portable-Renderprüfungen
und einen isolierten tatsächlichen Portable-Start. Die Release-Pipeline baut
frische Pakete und wiederholt diese automatisierten Prüfungen vor Veröffentlichung.
Echter Austausch über Dropbox, OneDrive oder Proton Drive zwischen zwei PCs,
WoW-Abnahme und automatisches Update von einer öffentlichen Vorversion sind nicht
vollständig praktisch geprüft. Für den Store bleiben vollständige Einrichtung/
Übernahme, Tray-Bedienung danach und Autostart nach Windows-Anmeldung offen.
[Prüfnachweise und Grenzen](https://github.com/krebs3r/hourstone-companion/blob/v0.2.0/docs/store/CERTIFICATION-FIX-2026-09-24.md).

## English

**Before updating:** Upgrade all Companions in a synchronization group to **0.2.0
together**. New cloud snapshots use protocol 4 and cannot be read by older
Companions. Existing snapshots remain readable; settings, playtime, visibility
and the sync group are retained. The local database migrates to schema 2;
older Companions can no longer open that profile after migration.

### New and improved

- Retail progress with the owned keystone, weekly best and all nine Great Vault
  slots. Character details show source, difficulty and time, distinguishing
  unknown, confirmed-empty and outdated values.
- Progress exchange between your PCs and back into the addon. Each progress
  family keeps its own source and timestamp; received values are never
  republished as local observations.
- Clear guidance for cloud files that are not yet locally available, including
  Proton Drive. Files are checked again when readable; valid cached data survives
  read errors.
- Local diagnostics under **Synchronization**, improved credits and corrected
  mouse-focus behavior for title-bar buttons.
- Store preparation with a separate profile, Store updates, optional data transfer
  and guidance for manually disabling the previous edition's startup. Direct
  installations continue to use GitHub updates.

**Addon:** Playtime, guilds and visibility require 0.2.2 or later; local Retail
progress requires 0.3.1 or later. Sending progress back into WoW requires at least
0.3.2 with its protocol-4 capability.
[Download addon 0.3.3](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.3).

**Unsigned GitHub packages:** Windows may show an unknown publisher or block
installation. Windows Code Integrity blocked the installer during an earlier
local setup test. Fully extract the portable ZIP and start its root launcher.

**Store status:** Package 1.0.1.0 (product 0.2.0) was resubmitted for certification
on 25 September 2026. Approval is pending. This GitHub release does not establish
Store availability.

**Validation:** 341 .NET tests and 40 Python checks passed. Existing package
validation covers 125 extended render cases, package/portable render checks and
an isolated real portable startup. The release pipeline builds fresh packages
and repeats these automated checks before publication. Real two-PC exchange
through Dropbox, OneDrive or Proton Drive, native WoW acceptance and automatic
updating from a public previous version have not been fully practically verified.
Full Store setup/transfer, subsequent tray operation and startup after Windows
sign-in remain unverified.
[Validation evidence and limits](https://github.com/krebs3r/hourstone-companion/blob/v0.2.0/docs/store/CERTIFICATION-FIX-2026-09-24.md).
