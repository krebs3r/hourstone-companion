# Hourstone Companion 0.1.6

**[Windows-Installer herunterladen](https://github.com/krebs3r/hourstone-companion/releases/download/v0.1.6/HourstoneCompanion-win-Setup.exe)** · **[Portable herunterladen](https://github.com/krebs3r/hourstone-companion/releases/download/v0.1.6/HourstoneCompanion-win-Portable.zip)**

Windows 11 x64 · Für die normale Installation den Installer verwenden. Das Portable-Paket vor dem Start vollständig entpacken.

## Deutsch

Hourstone Companion ergänzt das WoW-Addon optional um eine App für **Windows 11
x64**. Sie zeigt gespeicherte Spielzeit, Klassen und Gilden über Retail, Mists
Classic, TBC Anniversary und Classic Era hinweg an. Charaktere und Gilden lassen
sich suchen; Client- und Realmfilter helfen bei der Übersicht.

Die App liest ausgewählte WoW-Accounts lokal ein und kann die Daten eigener PCs
über einen dauerhaft lokal verfügbaren Dropbox-, OneDrive- oder anderen
Synchronisierungsordner austauschen. Ein eigenes Companion-Konto ist nicht nötig.
Aus der Übersicht gelöschte Einträge lassen sich wiederherstellen; WoW-Charaktere
und gespeicherte Spielzeit bleiben erhalten. Das Addon funktioniert auch ohne App.

**Neu in 0.1.6:**

- Der senkrechte Trennstrich zwischen Navigation und Inhalt entfällt.
- Ein dezenter Mülleimer rechts neben „Aktualisiert“ ersetzt den Löschbutton unter der Tabelle. Jede Zeile lässt sich direkt löschen; die Bestätigung bleibt erhalten.
- Gelöschte Charaktere erhalten an derselben Stelle ein Wiederherstellen-Symbol. Beide Aktionen unterstützen Tastaturfokus und deutsche sowie englische Tooltips.
- Der Text unter „Lokale Diagnose“ verwendet Consolas.

Benötigt **Hourstone 0.2.2 oder neuer**. Einstellungen, Spielzeitdaten und
Wiederherstellungen bleiben erhalten; das Synchronisierungsprotokoll bleibt bei
Version 3. Falls CurseForge noch eine ältere Addon-Version anbietet, verwende
[Hourstone 0.2.3 auf GitHub](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.2.3).

**Unsignierte frühe Version.** Installer und Portable-Paket enthalten die
.NET-Laufzeit. Windows kann einen unbekannten Herausgeber anzeigen. Lade die
Pakete aus diesem Repository herunter; Prüfsummen liegen dem Release bei.

Release-Build und 242 automatisierte Tests sind erfolgreich. Die Oberfläche wurde
in Hell/Dunkel bei 100 %, 150 % und 200 % sowie in schmalen Fenstern geprüft.
Löschen, Abbrechen, Wiederherstellen und die Sperre während des Abgleichs wurden
mit Beispieldaten geprüft. Installer- und Portable-Pakete wurden erstellt; die
Anwendung aus beiden Paketstufen wurde durch Renderprüfungen validiert.
Ein lokales Upgrade von 0.1.5 auf 0.1.6 wurde noch nicht praktisch geprüft.
Der vollständige Austausch über Dropbox oder OneDrive zwischen zwei PCs ist
noch nicht praktisch geprüft; eine erfolgreiche automatische Updateinstallation
von einer zuvor veröffentlichten Version wurde noch nicht nachgewiesen.

## English

**[Download Windows installer](https://github.com/krebs3r/hourstone-companion/releases/download/v0.1.6/HourstoneCompanion-win-Setup.exe)** · **[Download portable](https://github.com/krebs3r/hourstone-companion/releases/download/v0.1.6/HourstoneCompanion-win-Portable.zip)**

Use the installer for a normal installation. Fully extract the portable package before running it.

Hourstone Companion is an optional **Windows 11 x64** app for the WoW addon.
Browse saved playtime, classes and guilds across Retail, Mists Classic, TBC
Anniversary and Classic Era, with search and client/realm filters. Read selected
accounts locally and synchronize your own PCs through a locally available Dropbox,
OneDrive or other synchronized folder, without a Companion account. Reversible
deletion from the overview preserves the WoW character and saved playtime. The
addon also works independently.

Version 0.1.6 removes the vertical separator between navigation and content. A
subtle trash icon to the right of **Updated** replaces the delete button below
the table. Each row can be deleted directly, with confirmation. **Deleted
characters** uses a restore icon in the same position. Both actions support
keyboard focus and German/English tooltips. **Local diagnostics** uses Consolas.

Requires **Hourstone 0.2.2 or later**. Protocol 3, existing settings, observations
and restoration controls are preserved. If CurseForge still offers an older addon,
use [Hourstone 0.2.3 on GitHub](https://github.com/krebs3r/hourstone-azeroth-hours/releases/tag/v0.2.3).

**Unsigned early release.** The installer and portable package include the .NET
runtime. Windows may show an unknown publisher. Download from this repository;
checksums are included with the release.

The Release build and all 242 automated tests passed. Dark/light layouts were
checked at 100%, 150% and 200%, including compact windows. Deletion, cancellation,
restoration and blocking actions during synchronization were verified with sample
data. Installer and portable packages were built, with render checks of the
staged and extracted portable applications. A local 0.1.5-to-0.1.6 upgrade,
end-to-end Dropbox/OneDrive exchange between two PCs and automatic update
installation from a previously published version have not yet been practically
verified.
