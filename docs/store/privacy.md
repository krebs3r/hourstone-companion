# Hourstone Companion – Datenschutzhinweise / Privacy information

Stand / Updated: 2026-09-25 · Produktversion / Product version: 0.2.0

## Deutsch

### Anwendung und Kontakt

Hourstone Companion wird von Martin Krebs Software als unabhängiges, quelloffenes Projekt veröffentlicht. Support: mail@martin-krebs.eu. Projektkontakt und Quellcode: [Hourstone-Companion-Repository](https://github.com/krebs3r/hourstone-companion). Die folgenden Hinweise beschreiben die Datenverarbeitung durch die Anwendung. Windows, Microsoft Store, WoW und ein von dir gewählter Cloud-Dienst unterliegen zusätzlich ihren eigenen Datenschutzhinweisen.

### Daten auf deinem PC

Die App liest die gespeicherten Daten des Hourstone-Addons aus den ausgewählten WoW-Accountordnern. Dazu gehören Charakterkennung, Name, Realm, Region, WoW-Client, Klasse, Stufe, Gilde oder bestätigte Gildenlosigkeit, Spielzeit sowie Erfassungszeitpunkte. Bei Retail kommen Schlüsselstein, Wochenbestwert, Saisonkennung und Schatzkammerfortschritt mit Schwierigkeitsangaben, Schwellenwerten und Reset-Zeitpunkten hinzu. Entscheidungen zum Ausblenden und Wiederherstellen werden ebenfalls gespeichert.

Die lokale SQLite-Datenbank enthält diese Beobachtungen, zwischengespeicherte Daten anderer verbundener PCs, Geräte- und Gruppenkennungen, Revisionsstände sowie die Konfiguration. Zur Konfiguration gehören die ausgewählten WoW-Pfade, lokalen Accountordnernamen, der Sync-Ordner und dessen Pausenstatus. Erscheinungsbild, Sprache und Autostartwahl werden lokal gespeichert. Die Gerätebezeichnung entspricht zunächst dem Windows-PC-Namen und kann in den Einstellungen geändert werden. Kennungen und Spieldaten können wiedererkennbar und einer Person zuordenbar sein; sie sind nicht als anonyme Daten zu verstehen.

Die Store-Ausgabe verwendet den lokalen App-Datenordner ihres Windows-Pakets. Die Direktversion verwendet `%LocalAppData%\Hourstone\Companion`. Bei einer bestätigten Übernahme in die Store-Ausgabe werden Daten und Einstellungen kopiert und Sicherungen unter `import-backups` im Store-Profil angelegt. Die ursprünglichen Profildaten bleiben erhalten. Die Store-Ausgabe erhält dabei eine neue Gerätekennung.

Die App schreibt ein verwaltetes Datenaddon in die ausgewählten WoW-Installationen, damit WoW die zusammengeführten Daten anzeigen kann. Sie überschreibt dabei keine Hourstone-SavedVariables. Lokale Diagnoseanzeigen können Accountordner, vollständige Dateipfade, Quellenkennungen und technische Fehlermeldungen enthalten. Sie werden nicht automatisch an den Projektentwickler übertragen.

### Optionaler Austausch zwischen deinen PCs

Erst nach Auswahl eines gemeinsamen Ordners schreibt die App dort unter `HourstoneSync` eine Gruppenkennung und eine Snapshot-Datei je Gerät. Diese Dateien enthalten Gerätekennung und -bezeichnung, Revisionen, die ausgewählten Charakterbeobachtungen einschließlich Gilde, Spielzeit und Retail-Fortschritt sowie Ausblende-/Wiederherstellungsentscheidungen mit ihren Kennungen. Die lokalen Accountordnernamen, Installationspfade und Darstellungseinstellungen gehören nicht zum Snapshot-Format.

Die Dateien liegen in lesbarem JSON vor; Hourstone fügt keine eigene Verschlüsselung hinzu. Dein Dateisynchronisierungsdienst überträgt sie entsprechend deiner Einrichtung. Personen und Geräte mit Zugriff auf den Ordner können sie lesen. Hourstone verwendet keine direkte Dropbox-, OneDrive- oder Proton-Drive-Anmeldung und erhält keine Zugangsdaten dieser Dienste. Es gibt keinen Hourstone-Synchronisierungsserver.

### Netzwerk, Updates und externe Links

Die Anwendung enthält keine eigene Werbe-, Analyse- oder Telemetrieübertragung an den Projektentwickler. Die Store-Ausgabe initialisiert den GitHub-Updater nicht; ihre Updates werden über Microsoft Store verwaltet. Die separat vertriebene Direktversion prüft öffentliche GitHub-Releases und lädt dort Updates herunter. Dabei entstehen normale Verbindungsdaten bei den beteiligten Diensten. Charakterdaten werden vom Updatevorgang nicht als Updateanfrage übertragen.

Links zu CurseForge, GitHub, Microsoft Store oder Windows-Einstellungen werden erst durch die entsprechende Aktion geöffnet. Für geöffnete Websites und externe Dienste gelten deren eigene Datenverarbeitungen. Die App fragt weder Battle.net-Passwörter noch Cloud-Zugangsdaten ab.

### Aufbewahrung und deine Auswahl

Gespeicherte Beobachtungen und Sicherungen haben keine automatische Ablaufzeit. Nicht lesbare oder ältere Eingabedateien können dazu führen, dass die letzte gültige Beobachtung erhalten bleibt. Du kannst Quellen abwählen, den Austausch pausieren oder den Sync-Ordner trennen. Pausieren behält bereits empfangene Daten. Trennen entfernt empfangene Geräte-Snapshots aus dem aktiven lokalen Abgleich, löscht aber keine Dateien bei anderen PCs oder im Cloud-Ordner. Gelernte Ausblende-/Wiederherstellungsentscheidungen können lokal erhalten bleiben.

„Charakter löschen“ in der Übersicht blendet einen Eintrag aus und ist keine vollständige Datenlöschung. Seine Beobachtungen bleiben für eine Wiederherstellung erhalten und können weiter synchronisiert werden. Für die vollständige Entfernung gespeicherter Kopien sind die lokalen App-Daten und Sicherungen, erzeugte Datenaddons, die ursprünglichen Addon-Daten sowie Kopien im gemeinsamen Ordner und auf anderen PCs getrennt zu berücksichtigen. Beende zuvor den laufenden Abgleich. Aufbewahrung und Versionshistorien eines Cloud-Dienstes werden durch dessen Einstellungen bestimmt.

Wenn du Support nutzt, werden nur die Angaben weitergegeben, die du selbst übermittelst. Veröffentliche keine unbearbeiteten SavedVariables, Snapshots oder Diagnosen mit privaten Pfaden in öffentlichen Meldungen.

## English

### Application and contact

Hourstone Companion is published by Martin Krebs Software as an independent open-source project. Support: mail@martin-krebs.eu. Project contact and source code: [Hourstone Companion repository](https://github.com/krebs3r/hourstone-companion). This notice describes processing performed by the application. Windows, Microsoft Store, WoW and your chosen cloud service also have their own privacy information.

### Data on your PC

The app reads saved Hourstone addon data from the WoW account folders you select. This includes character identifier, name, realm, region, WoW client, class, level, guild or confirmed absence of a guild, playtime and observation timestamps. Retail data also includes keystone, weekly best, season identifier and Great Vault progress with difficulty descriptions, thresholds and reset timestamps. Hide and restore decisions are stored as well.

The local SQLite database contains these observations, cached data from connected PCs, device and group identifiers, revisions and configuration. Configuration includes selected WoW paths, local account-folder names, the sync folder and its pause state. Appearance, language and startup preferences are stored locally. The device display name initially uses the Windows PC name and can be changed in Settings. Identifiers and game data can be recognizable and linked to a person; they should not be considered anonymous.

The Store edition uses its Windows package's local application data folder. The direct edition uses `%LocalAppData%\Hourstone\Companion`. A confirmed transfer to the Store edition copies data and preferences and retains backups under `import-backups` in the Store profile. The original profile data remains. The Store edition receives a new device identifier during transfer.

The app writes a managed data addon into selected WoW installations so WoW can display combined data. It does not overwrite Hourstone SavedVariables. Local diagnostic views can contain account-folder names, complete file paths, source identifiers and technical errors. They are not automatically sent to the project developer.

### Optional exchange between your PCs

After you select a shared folder, the app writes a group identifier and one snapshot file per device under `HourstoneSync`. These files contain device identifier and display name, revisions, selected character observations including guild, playtime and Retail progress, and hide/restore decisions with their identifiers. Local account-folder names, installation paths and appearance preferences are not part of the snapshot format.

The files are readable JSON; Hourstone adds no encryption of its own. Your file synchronization service transfers them according to your setup. People and devices with access to the folder can read them. Hourstone does not sign in directly to Dropbox, OneDrive or Proton Drive and does not receive their credentials. There is no Hourstone synchronization server.

### Network, updates and external links

The application contains no advertising, analytics or telemetry transmission of its own to the project developer. The Store edition does not initialize the GitHub updater; Microsoft Store manages its updates. The separately distributed direct edition checks public GitHub releases and downloads updates there. The services involved receive normal connection information. Character data is not included in update requests.

Links to CurseForge, GitHub, Microsoft Store or Windows settings open through the corresponding user action. Opened websites and external services perform their own data processing. The app does not request Battle.net passwords or cloud-service credentials.

### Retention and your choices

Saved observations and backups have no automatic expiry. Unreadable or older input files can cause the last valid observation to be retained. You can deselect sources, pause exchange or disconnect the sync folder. Pausing keeps previously received data. Disconnecting removes received device snapshots from active local synchronization but does not delete files on other PCs or in the cloud folder. Learned hide/restore decisions can remain locally.

“Delete character” in the overview hides a record; it does not completely erase its data. Observations remain available for restoration and may continue synchronizing. Complete removal of stored copies requires separately considering local app data and backups, generated data addons, original addon data, and copies in the shared folder and on other PCs. Stop active synchronization first. Cloud retention and version history depend on that service's settings.

If you request support, only information you choose to submit is shared. Do not post unredacted SavedVariables, snapshots or diagnostics containing private paths in public reports.

---

Historical submission record: on 2026-09-18, the then-current German and English text was saved in Partner Center using its **Privacy policy text** option, without Markdown formatting or this internal record. That submission failed certification on 2026-09-21. The current revision adds the support email; its upload status is recorded in `artifacts/store-submission/partner-center-status.json`. Publisher display name: **Martin Krebs Software**. Submission does not constitute privacy/legal approval or a successful certification result.
