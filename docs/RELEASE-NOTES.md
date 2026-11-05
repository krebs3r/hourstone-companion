# Hourstone Companion 0.2.2

Windows 11 x64 · .NET enthalten / .NET included

- [Windows-Installer herunterladen / Download Windows installer](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.2/HourstoneCompanion-win-Setup.exe)
- [Portable-Ausgabe herunterladen / Download portable edition](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.2/HourstoneCompanion-win-Portable.zip)
- [SHA-256-Prüfsummen / SHA-256 checksums](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.2/SHA256SUMS)

## Deutsch

**Vor dem Update:** 0.2.2 ändert weder Sync-Protokoll 4 noch Datenbankschema 2
noch die Einstellungen. PCs mit 0.2.0, 0.2.1 und 0.2.2 gleichen weiter miteinander
ab und müssen nicht gemeinsam aktualisiert werden.

### Neu und geändert

- **WoW: Forever auch im Spiel:** Das Datenaddon **Hourstone_Sync** nennt jetzt die
  Forever-Installationen, die eigenen und die, die andere PCs über ihre
  Client-Hinweise melden. Hourstone 0.3.7 zeigt damit synchronisierte
  Forever-Charaktere anderer Accounts und PCs in jedem Client als „WoW: Forever“,
  auch in Forever selbst, und bietet dort den Clientfilter WoW: Forever an. Ältere
  Hourstone-Versionen ignorieren die Liste und führen diese Charaktere weiter als
  Retail. Ohne Forever-Installation bleibt das Datenaddon Byte für Byte wie bei
  0.2.1 ([Beschreibung](https://github.com/krebs3r/hourstone-companion/blob/v0.2.2/docs/sync-protocol-v4-forever.md)).
- **Kopierte WTF-Ordner:** Verwendet ein anderer Client dieselbe Hourstone-Daten-ID
  wie eine Forever-Installation, gilt die Forever-Installation als Kopie und tritt
  wie beim Hinzufügen von Installationen hinter den anderen Client zurück. Ihre
  Charaktere werden dann weder im Companion noch im Spiel als Forever
  gekennzeichnet; 0.2.1 zeigte sie auf anderen PCs trotzdem als Forever an.
- Die Charakterliste kennzeichnet einen Charakter nach der Quelle seiner gültigen
  Spielzeitbeobachtung und zeigt damit dieselben Forever-Charaktere wie das Addon.

**Addon:** [Hourstone 0.3.7](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.7)
liest die Liste und erscheint separat. Für selbst erfasste Charaktere entscheidet
dort weiter die Kennzeichnung des eigenen Accounts. Mit Hourstone 0.3.6 oder älter
ändert sich im Spiel nichts. Ansonsten gelten die bisherigen Anforderungen: Forever
ab 0.3.5, Spielzeit, Gilden und Sichtbarkeit ab 0.2.2, lokaler Retail-Fortschritt
ab 0.3.1, der Rückweg ins Spiel ab 0.3.2 mit Protokoll-4-Kennung.

**Installation und Update:** Installierte Kopien und vollständig entpackte
Portable-Ausgaben aktualisieren sich automatisch über GitHub. Das Update wartet
unter anderem, bis WoW geschlossen ist; **Nach App-Updates suchen** prüft sofort.

**Unsignierte GitHub-Pakete:** Windows kann einen unbekannten Herausgeber anzeigen
oder die Installation blockieren. Beim früheren lokalen Setup-Test wurde der
Installer von Windows Code Integrity blockiert. Die Portable-ZIP vollständig
entpacken und über den enthaltenen Rootlauncher starten.

**Store-Ausgabe:** Die Microsoft-Store-Ausgabe wird separat eingereicht und
benötigt eine Zertifizierung. Dieses GitHub-Release bedeutet keine
Store-Veröffentlichung; Store-Kopien erhalten 0.2.2 erst nach der Freigabe über
den Microsoft Store.

**Validierung:** 372 .NET-Tests bestanden, darunter neue Tests für die
Forever-Liste im Datenaddon, kopierte WTF-Ordner auf anderen PCs und mit dem Addon
gemeinsame Fixtures, einschließlich der erzeugten Daten Byte für Byte. Die
Release-Pipeline baut frische Pakete und führt vor der Veröffentlichung diese Tests
erneut aus, dazu die Python-Prüfungen, die erweiterten Render- und Paketprüfungen und
den isolierten Portable-Start. Die Anzeige im Spiel mit Hourstone 0.3.7, echter
Austausch über Dropbox, OneDrive oder Proton Drive zwischen zwei PCs und das
automatische Update von 0.2.1 auf 0.2.2 sind noch nicht praktisch geprüft.

## English

**Before updating:** 0.2.2 does not change sync protocol 4, database schema 2 or
settings. PCs running 0.2.0, 0.2.1 and 0.2.2 keep exchanging data and do not need
to be upgraded together.

### New and changed

- **WoW: Forever in the game, too:** The **Hourstone_Sync** data addon now names the
  Forever installations, this PC's own and those that other PCs report through
  their client hints. Hourstone 0.3.7 uses it to show synchronized Forever
  characters of other accounts and PCs as “WoW: Forever” in every client, Forever
  included, and offers the WoW: Forever client filter there. Older Hourstone
  versions ignore the list and keep listing these characters as Retail. Without a
  Forever installation, the data addon stays byte for byte as in 0.2.1
  ([description](https://github.com/krebs3r/hourstone-companion/blob/v0.2.2/docs/sync-protocol-v4-forever.md)).
- **Copied WTF folders:** When another client uses the same Hourstone data ID as a
  Forever installation, the Forever installation counts as the copy and yields to
  the other client, as it does when installations are added. Its characters are
  then labelled Forever neither in the Companion nor in the game; 0.2.1 still
  labelled them Forever on other PCs.
- The character list labels a character by the source of its winning playtime
  observation and so shows the same Forever characters as the addon.

**Addon:** [Hourstone 0.3.7](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.7)
reads the list and is released separately. For characters an account recorded
itself, that account's own label still decides there. With Hourstone 0.3.6 or
earlier, nothing changes in the game. All other requirements are unchanged:
Forever needs 0.3.5 or later; playtime, guilds and visibility 0.2.2 or later; local
Retail progress 0.3.1 or later; and sending progress back into WoW at least 0.3.2
with its protocol-4 capability.

**Installation and updates:** Installed copies and fully extracted portable
packages update automatically from GitHub. Among other conditions, the update
waits until WoW is closed; **Check for app updates** checks immediately.

**Unsigned GitHub packages:** Windows may show an unknown publisher or block
installation. Windows Code Integrity blocked the installer during an earlier
local setup test. Fully extract the portable ZIP and start its root launcher.

**Store edition:** The Microsoft Store edition of 0.2.2 will be submitted
separately and requires certification. This GitHub release does not establish Store
availability; Store copies receive 0.2.2 through Microsoft Store only after
approval.

**Validation:** 372 .NET tests passed, including new tests for the Forever list in
the data addon, copied WTF folders on other PCs and fixtures shared with the addon,
including the generated data byte for byte. The release pipeline builds fresh
packages and, before publication, runs these tests again along with the Python
checks, extended render and package checks and the isolated portable startup. The
in-game labels with Hourstone 0.3.7, real two-PC exchange through Dropbox, OneDrive
or Proton Drive, and the automatic update from 0.2.1 have not been practically
verified yet.
