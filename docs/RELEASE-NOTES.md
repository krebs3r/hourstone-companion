# Hourstone Companion 0.2.1

Windows 11 x64 · .NET enthalten / .NET included

- [Windows-Installer herunterladen / Download Windows installer](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.1/HourstoneCompanion-win-Setup.exe)
- [Portable-Ausgabe herunterladen / Download portable edition](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.1/HourstoneCompanion-win-Portable.zip)
- [SHA-256-Prüfsummen / SHA-256 checksums](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.1/SHA256SUMS)

## Deutsch

**Vor dem Update:** 0.2.1 ändert weder Sync-Protokoll 4 noch Datenbankschema 2
noch die Einstellungen. PCs mit 0.2.0 und 0.2.1 gleichen weiter miteinander ab und
müssen nicht gemeinsam aktualisiert werden; Companions mit 0.2.0 zeigen
Forever-Charaktere weiterhin als Retail. Nach dem Update WoW einmal vollständig
neu starten, damit es das Datenaddon mit den neuen Interface-Angaben erkennt.

### Neu und behoben

- **WoW: Forever** erscheint als eigener Client. Installationen im Clientordner
  `_forever_` (neben `_retail_`, mit derselben `Wow.exe`) heißen auf den
  Clientkarten, in der Charakterliste, im Clientfilter und in der Diagnose
  „WoW: Forever“. Forever-Charaktere stehen nicht mehr in der
  Retail-Fortschrittsansicht. Im Sync-Protokoll 4 bleibt Forever ohne
  Formatänderung Teil der Retail-Familie; das Addon im Spiel führt diese
  Charaktere deshalb weiter als Retail.
- Andere PCs mit 0.2.1 übernehmen die Forever-Bezeichnung über eine kleine
  Hinweisdatei `<Geräte-ID>.clients` im Sync-Ordner
  ([Beschreibung](https://github.com/krebs3r/hourstone-companion/blob/v0.2.1/docs/sync-client-hints.md)).
  Ältere Companions ignorieren sie und zeigen diese Charaktere weiter als Retail.
- Das Datenaddon **Hourstone_Sync** gibt die Interfaces 120105, 50504, 20506,
  11509 und 16001 an. WoW 12.1.5 und WoW: Forever führen es nicht mehr als
  veraltet. Das in den Hinweisen zu Hourstone 0.3.4 empfohlene **Laden veralteter
  AddOns** ist dafür nicht mehr nötig.
- Ein kopierter WTF-Ordner, also zwei Installationen mit derselben
  Hourstone-Daten-ID, beendet die App nicht mehr mit „Hourstone Companion konnte
  nicht starten“. Die Kopie wird nicht gelesen, und ein Hinweis erklärt den Grund.
  Eine Forever-Kopie tritt hinter den anderen Client zurück, auch wenn die
  Forever-Installation zuvor hinzugefügt wurde.
- Charaktere, die eine ältere Hourstone-Version für einen dem Companion
  unbekannten Client gespeichert hat, fehlen nicht mehr kommentarlos. Ein Beispiel
  sind Beta-Charaktere aus WoW: Forever, die Hourstone 0.3.3 als „project-18“
  abgelegt hat. Ein Hinweis führt zur Seite Clients; Clientkarte und Diagnose
  nennen beide Abhilfen: Bei
  Beta-Charakteren WoW schließen und `SavedVariables\Hourstone.lua` aus diesem
  Client herausverschieben; andernfalls Hourstone aktualisieren, sich einmal
  einloggen und danach ausloggen oder `/reload` ausführen.
- Die Retail-Fortschrittstabelle füllt ein maximiertes Fenster. Bei geöffneten
  Details oder in kleinen Fenstern scrollt die Seite, und die Tabelle behält ihre
  Zeilen.

**WoW: Forever:** Forever startet am 5. November 2026 um 0:00 Uhr MEZ
(4. November, 23:00 UTC). Beta-Charaktere werden nicht ins Live-Spiel übernommen.
Wer den WTF-Ordner der Beta nach `_forever_` kopiert hat, verschiebt vor dem
ersten Live-Login bei geschlossenem WoW die Datei `Hourstone.lua` aus
`_forever_\WTF\Account\*\SavedVariables`, statt sie zu löschen. Sonst bleiben die
Beta-Charaktere in der Liste.

**Addon:** Zeitgleich erscheint Hourstone 0.3.5. Auf Forever ordnet es jeden
Charakter der Retail-Familie zu, holt Charaktere, die eine frühere Version unter
einem unbekannten Client wie „project-18“ abgelegt hat, ohne doppelte Spielzeit
zu Retail zurück und speichert nie einen leeren Realm oder Namen. Der Companion
lehnt einen leeren Realm ab; ein solcher Eintrag würde sonst den ganzen Account im
Companion blockieren. Für Forever deshalb Addon 0.3.5 verwenden. Ansonsten gelten
die bisherigen Anforderungen: Spielzeit, Gilden und Sichtbarkeit ab 0.2.2, lokaler
Retail-Fortschritt ab 0.3.1, der Rückweg ins Spiel ab 0.3.2 mit
Protokoll-4-Kennung.
[Addon 0.3.5 herunterladen](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.5).

**Installation und Update:** Installierte Kopien und vollständig entpackte
Portable-Ausgaben aktualisieren sich automatisch über GitHub. Das Update wartet
unter anderem, bis WoW geschlossen ist; **Nach App-Updates suchen** prüft sofort.

**Unsignierte GitHub-Pakete:** Windows kann einen unbekannten Herausgeber anzeigen
oder die Installation blockieren. Beim früheren lokalen Setup-Test wurde der
Installer von Windows Code Integrity blockiert. Die Portable-ZIP vollständig
entpacken und über den enthaltenen Rootlauncher starten.

**Store-Ausgabe:** Die Microsoft-Store-Ausgabe wird separat eingereicht und
benötigt eine Zertifizierung. Dieses GitHub-Release bedeutet keine
Store-Veröffentlichung; Store-Kopien erhalten 0.2.1 erst nach der Freigabe über
den Microsoft Store.

**Validierung:** 364 .NET-Tests bestanden, darunter neue Tests für die
Forever-Erkennung, Client-Hinweise, kopierte WTF-Ordner und Charaktere unbekannter
Clients. Die Release-Pipeline baut frische Pakete und führt vor der
Veröffentlichung diese Tests erneut aus, dazu die Python-Prüfungen, die erweiterten
Render- und Paketprüfungen und den isolierten Portable-Start. Der Live-Client von
WoW: Forever wurde noch nicht getestet; Interface 16001 ist nur auf Beta- und
Testbuilds von Forever bestätigt. Echter Austausch über Dropbox, OneDrive oder
Proton Drive zwischen zwei PCs, einschließlich der Client-Hinweise, und das
automatische Update von 0.2.0 auf 0.2.1 sind weiterhin nicht praktisch geprüft.

## English

**Before updating:** 0.2.1 does not change sync protocol 4, database schema 2 or
settings. PCs running 0.2.0 and 0.2.1 keep exchanging data and do not need to
be upgraded together; Companions running 0.2.0 continue to show Forever
characters as Retail. After updating, restart WoW completely once so it picks up
the data addon's new interface list.

### New and fixed

- **WoW: Forever** appears as its own client. Installations in the `_forever_`
  client folder (next to `_retail_`, with the same `Wow.exe`) are shown as
  “WoW: Forever” on client cards, in the character list, the client filter and
  diagnostics. Forever characters are no longer listed in the Retail progress
  view. In sync protocol 4, Forever stays in the Retail family without a format
  change, so the in-game addon still lists these characters as Retail.
- Other PCs running 0.2.1 learn the Forever label from a small `<deviceId>.clients`
  hint file in the sync folder
  ([description](https://github.com/krebs3r/hourstone-companion/blob/v0.2.1/docs/sync-client-hints.md)).
  Older Companions ignore it and keep showing these characters as Retail.
- The **Hourstone_Sync** data addon declares interfaces 120105, 50504, 20506,
  11509 and 16001. WoW 12.1.5 and WoW: Forever no longer list it as out of date,
  so the **Load out of date AddOns** setting recommended in the Hourstone 0.3.4
  notes is no longer needed for it.
- A copied WTF folder, meaning two installations with the same Hourstone data ID,
  no longer closes the app with “Could not start”. The copy is not read and a
  notice explains why. A Forever copy yields to the other client, even if the
  Forever installation was added first.
- Characters that an older Hourstone saved for a client the Companion does not
  know are no longer silently left out. One example is WoW: Forever beta
  characters that Hourstone 0.3.3 saved as “project-18”. A notice points to the
  Clients page; the Clients card and diagnostics explain both remedies: for beta characters, close WoW and
  move `SavedVariables\Hourstone.lua` out of this client; otherwise update
  Hourstone, log in once, then log out or use `/reload`.
- The Retail progress table now fills a maximized window. With details open or in
  small windows, the page scrolls and the table keeps its rows.

**WoW: Forever:** Forever goes live on 4 November 2026 at 23:00 UTC (00:00 CET on
5 November). Beta characters do not carry over to live. If you copied the beta's
WTF folder into `_forever_`, close WoW and move `Hourstone.lua` out of
`_forever_\WTF\Account\*\SavedVariables` before your first live login, rather than
deleting it. Otherwise the beta characters stay in the list.

**Addon:** Hourstone 0.3.5 is released alongside. On Forever it assigns every
character to the Retail family, returns characters that an earlier version filed
under an unknown client such as “project-18” to Retail without counting playtime
twice, and never saves an empty realm or name. The Companion rejects an empty
realm, which would otherwise block the whole account in the Companion. Use addon
0.3.5 on Forever. All other requirements are unchanged: playtime, guilds and
visibility require 0.2.2 or later, local Retail progress 0.3.1 or later, and
sending progress back into WoW at least 0.3.2 with its protocol-4 capability.
[Download addon 0.3.5](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.5).

**Installation and updates:** Installed copies and fully extracted portable
packages update automatically from GitHub. Among other conditions, the update
waits until WoW is closed; **Check for app updates** checks immediately.

**Unsigned GitHub packages:** Windows may show an unknown publisher or block
installation. Windows Code Integrity blocked the installer during an earlier
local setup test. Fully extract the portable ZIP and start its root launcher.

**Store edition:** The Microsoft Store edition of 0.2.1 will be submitted
separately and requires certification. This GitHub release does not establish Store
availability; Store copies receive 0.2.1 through Microsoft Store only after
approval.

**Validation:** 364 .NET tests passed, including new tests for Forever detection,
client hints, copied WTF folders and characters of unknown clients. The release
pipeline builds fresh packages and, before publication, runs these tests again
along with the Python checks, extended render and package checks and the isolated
portable startup. The live WoW: Forever client has not been tested yet; interface
16001 is confirmed only on Forever beta and test builds. Real two-PC exchange
through Dropbox, OneDrive or Proton Drive, including client hints, and the
automatic update from 0.2.0 have still not been practically verified.
