# Hourstone Companion 0.2.3

Windows 11 x64 · .NET enthalten / .NET included

- [Windows-Installer herunterladen / Download Windows installer](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.3/HourstoneCompanion-win-Setup.exe)
- [Portable-Ausgabe herunterladen / Download portable edition](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.3/HourstoneCompanion-win-Portable.zip)
- [SHA-256-Prüfsummen / SHA-256 checksums](https://github.com/krebs3r/hourstone-companion/releases/download/v0.2.3/SHA256SUMS)

## Deutsch

**Vor dem Update:** 0.2.3 lässt Sync-Protokoll 4, Datenbankschema 2, die
Einstellungen und das Datenaddon **Hourstone_Sync** unverändert. PCs mit 0.2.0 bis
0.2.3 gleichen weiter miteinander ab und müssen nicht gemeinsam aktualisiert werden.
Das Update auf 0.2.3 selbst folgt noch den bisherigen Regeln, siehe **Installation
und Update**.

### Neu und geändert

- **Jetzt installieren:** Ist ein App-Update heruntergeladen, bieten der Hinweis in
  der App und **Einstellungen › Automatische Updates** die Schaltfläche **Jetzt
  installieren** an. Sie installiert das Update sofort, auch während WoW läuft, und
  startet die App danach sichtbar neu. Bei ungespeicherten Einstellungen oder
  während des Speicherns startet sie nicht; die Änderungen zuerst speichern oder
  rückgängig machen. Während einer Updateprüfung meldet sie, dass bereits eine
  Prüfung läuft. Einen laufenden Abgleich wartet sie ab und installiert danach.
- **Version und Zeitpunkt:** Der Hinweis, die Benachrichtigung im Infobereich
  und die Einstellungen nennen die Version und wann sie automatisch installiert
  wird: sobald WoW geschlossen ist und die App 90 Sekunden lang unbenutzt im
  Hintergrund liegt, also im Infobereich, minimiert oder während ein anderes
  Fenster aktiv ist. In den Einstellungen steht dazu „Version … ist
  heruntergeladen und wartet auf die Installation.“
- **Erneute Prüfung:** **Nach App-Updates suchen** meldet eine bereits
  heruntergeladene Version nicht noch einmal. Bisher begann damit die Wartezeit
  von 30 Sekunden nach dem Hinweis von vorn.
- Schlägt die Installation fehl, verschwindet der Status „App-Update
  heruntergeladen“ zusammen mit den Schaltflächen. Ein Klick auf eine
  Benachrichtigung im Infobereich öffnet das App-Fenster.
- Die automatische Installation selbst ist unverändert und startet die App weiter
  im Hintergrund neu.

**Addon:** Die Anforderungen bleiben unverändert. Aktuell ist
[Hourstone 0.3.7](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.7);
damit erscheinen synchronisierte Forever-Charaktere auch im Spiel als
„WoW: Forever“. Forever ab 0.3.5, Spielzeit, Gilden und Sichtbarkeit ab 0.2.2,
lokaler Retail-Fortschritt ab 0.3.1, der Rückweg ins Spiel ab 0.3.2 mit
Protokoll-4-Kennung.

**Installation und Update:** Installierte Kopien und vollständig entpackte
Portable-Ausgaben aktualisieren sich automatisch über GitHub; nur sie bieten
**Jetzt installieren**. **Das Update auf 0.2.3** wird noch von der bisher
installierten Version ausgeführt. Es folgt deshalb ihren Regeln und zeigt den
bisherigen Hinweis „Ein Update ist bereit …“ ohne Schaltfläche: Installiert wird,
sobald WoW geschlossen ist und die App etwa 90 Sekunden lang unbenutzt im
Hintergrund liegt. Ein erneuter Klick auf **Nach App-Updates suchen** startet diese
Wartezeit neu, ein Neustart der App installiert das Update nicht. **Jetzt
installieren** steht ab 0.2.3 für die darauf folgenden Updates bereit.

**Unsignierte GitHub-Pakete:** Windows kann einen unbekannten Herausgeber anzeigen
oder die Installation blockieren. Beim früheren lokalen Setup-Test wurde der
Installer von Windows Code Integrity blockiert. Die Portable-ZIP vollständig
entpacken und über den enthaltenen Rootlauncher starten.

**Store-Ausgabe:** Die Microsoft-Store-Ausgabe wird separat eingereicht und
benötigt eine Zertifizierung. Dieses GitHub-Release bedeutet keine
Store-Veröffentlichung; Store-Kopien erhalten 0.2.3 erst nach der Freigabe über
den Microsoft Store. Sie aktualisieren sich weiter über den Microsoft Store und
haben kein **Jetzt installieren**.

**Validierung:** 404 .NET-Tests bestanden, darunter neue Tests für die
Entscheidung bei **Jetzt installieren**, die Prüfung auf eine bereits
heruntergeladene Version, den gemeinsamen 90-Sekunden-Wert, die deutschen und
englischen Texte und die neue Render-Vorschau. Die erweiterten Render-Prüfungen
umfassen jetzt auch den Update-Hinweis und die Update-Karte der Einstellungen auf
Deutsch und Englisch und in kompakten Fenstern. Die Release-Pipeline baut frische
Pakete und führt vor der Veröffentlichung diese Tests erneut aus, dazu die
Python-Prüfungen, die erweiterten Render- und Paketprüfungen und den isolierten
Portable-Start. Am 6. November 2026 wurde auf einem PC das automatische Update von
0.2.1 auf 0.2.2 praktisch geprüft: Es wurde heruntergeladen, wartete, wurde
installiert und startete die App im Hintergrund neu (laut Velopack-Protokoll
erfolgreich angewendet). Noch nicht praktisch geprüft sind **Jetzt installieren**
mit einem echten Update (erst von 0.2.3 auf eine spätere Version möglich), das
Update von 0.2.2 auf 0.2.3, echter Austausch über Dropbox, OneDrive oder Proton
Drive zwischen zwei PCs und die Anzeige als WoW: Forever im Spiel mit Hourstone
0.3.7.

## English

**Before updating:** 0.2.3 does not change sync protocol 4, database schema 2,
settings or the **Hourstone_Sync** data addon. PCs running 0.2.0 to 0.2.3 keep
exchanging data and do not need to be upgraded together. The update to 0.2.3
itself still follows the previous rules; see **Installation and updates**.

### New and changed

- **Install now:** Once an app update has been downloaded, the notice in the app
  and **Settings › Automatic updates** offer **Install now**. It installs the update
  right away, even while WoW is running, and then restarts the app visibly. It does
  not start while settings are unsaved or being saved; save or undo the changes
  first. During an update check, it reports that a check is already running. It
  waits for a running sync and installs afterwards.
- **Version and timing:** The notice, the notification-area balloon and Settings
  name the version and when it installs automatically: once WoW is closed and the
  app has been left idle in the background for 90 seconds, meaning in the
  notification area, minimized or while another window is active. Settings also
  show “Version … has been downloaded and is waiting to install.”
- **Repeated check:** **Check for app updates** no longer announces an already
  downloaded version again. Previously, this restarted the 30-second delay after
  the notice.
- If the installation fails, the “App update downloaded” status disappears together
  with the buttons. Clicking a notification-area balloon opens the app window.
- Automatic installation itself is unchanged and still restarts the app in the
  background.

**Addon:** Requirements are unchanged. The current version is
[Hourstone 0.3.7](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.3.7),
which also shows synchronized Forever characters as “WoW: Forever” in the game.
Forever needs 0.3.5 or later; playtime, guilds and visibility 0.2.2 or later; local
Retail progress 0.3.1 or later; and sending progress back into WoW at least 0.3.2
with its protocol-4 capability.

**Installation and updates:** Installed copies and fully extracted portable
packages update automatically from GitHub; only they offer **Install now**.
**The update to 0.2.3** is still carried out by the previously installed version.
It therefore follows that version's rules and shows the previous notice “An update
is ready …” without a button: it installs once WoW is closed and the app has been
left idle in the background for about 90 seconds. Clicking **Check for app
updates** again restarts this wait, and restarting the app does not install the
update. **Install now** is available from 0.2.3 onward for the updates that follow.

**Unsigned GitHub packages:** Windows may show an unknown publisher or block
installation. Windows Code Integrity blocked the installer during an earlier
local setup test. Fully extract the portable ZIP and start its root launcher.

**Store edition:** The Microsoft Store edition of 0.2.3 will be submitted
separately and requires certification. This GitHub release does not establish Store
availability; Store copies receive 0.2.3 through Microsoft Store only after
approval. They keep updating through Microsoft Store and have no **Install now**.

**Validation:** 404 .NET tests passed, including new tests for the **Install now**
decision, the check for an already downloaded version, the shared 90-second value,
the German and English texts and the new render preview. The extended render checks
now also cover the update notice and the settings update card in German and English
and in compact windows. The release pipeline builds fresh packages and, before
publication, runs these tests again along with the Python checks, extended render
and package checks and the isolated portable startup. On 6 November 2026, the
automatic update from 0.2.1 to 0.2.2 was practically verified on one PC: it
downloaded, waited, installed and restarted the app in the background (the Velopack
log reports the package as applied successfully). **Install now** with a real
update (first possible from 0.2.3 to a later version), the update from 0.2.2 to
0.2.3, real two-PC exchange through Dropbox, OneDrive or Proton Drive, and the
in-game WoW: Forever labels with Hourstone 0.3.7 have not been practically verified
yet.
