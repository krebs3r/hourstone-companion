# Hourstone 0.2.0 – Prüfbericht

Aktueller Stand vom 24. September: [Zertifizierungsfix und Paket 1.0.1.0](store/CERTIFICATION-FIX-2026-09-24.md). Die Einreichung vom 18. September wurde am 21. September abgelehnt. Der folgende Bericht bleibt als historischer Nachweis erhalten.

Stand: 18. September 2026. Companion 0.2.0, Addon 0.3.2, Windows 11 x64
(Build 26200), .NET SDK 10.0.401. Die Direktpakete enthalten .NET/Windows Desktop
10.0.12. Der neue Kandidat wurde am **18.09.2026 um 15:01 UTC / 17:01 Uhr Berlin
zur Store-Zertifizierung eingereicht**. Portalstatus: **In certification**;
**Submission: Complete**, **Pre-processing: In progress**, **Certification:
Not started**, **Publishing: Not started**. Er ist noch nicht veröffentlicht.
Der neue Kandidat enthält einen sichtbaren Blizzard-Credit unter
**Einstellungen / Settings → Credits**. Direktpakete und Produktions-MSIX wurden
neu gebaut; vor dem Absenden war das neue Paket **Validated**, alle sechs
Abschnitte waren **Complete** und die acht Storeaufnahmen entsprachen dem Kandidaten.
WACK ist optional und wird übersprungen; Microsoft prüft die Capability bei der
Zertifizierung. Die ungeklärte Nachweislage der Blizzard-Assets bleibt dokumentiert.

## Ergebnis und konkrete Grenze

Die aktuelle portable Ausgabe
startet über ihren Rootlauncher, öffnet ein echtes WPF-Fenster, erstellt eine
isolierte Datenbank und schließt einen lokalen Abgleich ab. Das ist zusätzlich
zu 19 Renderprüfungen des neuen Direktbuilds geprüft. MakeAppx validiert das neu
gebaute Produktions-MSIX. Ein echtes Upgrade und der normale Start mit bestehender
Einrichtung wurden beim früheren 0.2.0-Build geprüft, nicht beim neuen Credits-Build.

**Der neue unsignierte Installer wurde gebaut, aber nicht ausgeführt oder als
Upgrade getestet.** Beim vorherigen Kandidaten blockierte Windows Code Integrity
den Setup-Start; Ereignisse 3077/3033 nannten die Signatur-/Code-Integrity-
Anforderung. Diese Blockade ist ein historischer Befund, kein Test des neuen
Installers. Die erfolgreiche Installation davor bleibt als funktionierende
0.2.0 installiert und läuft weiter. Sie enthält alle Funktionen und den
Autostart-Fix, aber noch nicht die abschließende Korrektur gegen vertikal
angeschnittene Reiter bei kleinen Fenstern und noch nicht den sichtbaren
Blizzard-Credit des aktuellen Kandidaten.

Die aktuelle portable Ausgabe, das Updatepaket und das MSIX enthalten die
Layoutkorrektur und den sichtbaren Credit. Ein erfolgreicher Start des neuen
Installers wird **nicht** behauptet. Es wurden keine
Windows-Schutzregeln oder Code-Integrity-Freigaben verändert. Für den separat
genehmigten nativen MSIX-Test wurde genau ein kurzlebiges Testzertifikat vorübergehend
unter LocalMachine/TrustedPeople vertraut und danach nachweislich wieder entfernt.
Eine vertrauenswürdige Signatur beziehungsweise eine administrativ verwaltete
Freigabe ist auf Systemen mit entsprechender Richtlinie weiterhin erforderlich.

## Gelieferte Dateien

| Datei | Zweck |
|---|---|
| `artifacts/releases/HourstoneCompanion-win-Setup.exe` | Neu gebauter unsignierter Windows-Installer mit sichtbarem Credit; Start/Upgrade nicht ausgeführt |
| `artifacts/releases/HourstoneCompanion-win-Portable.zip` | Vollständig entpacken; `Hourstone Companion.exe` starten; finaler Starttest bestanden |
| `artifacts/releases/HourstoneCompanion-0.2.0-full.nupkg` | Velopack-Updatepaket |
| `artifacts/releases/releases.win.json`, `RELEASES`, `assets.win.json` | Update- und Paketmetadaten |
| `artifacts/releases/SHA256SUMS`, `release-manifest.json` | Prüfsummen, Version und Signaturstatus |
| `artifacts/addon/Hourstone-0.3.2.zip` und `.zip.sha256` | Kompatibles WoW-Addon; auf Retail, Mists Classic und Classic Era mit Backups installiert; Anniversary noch 0.3.1 |
| `artifacts/msix/LocalTest/HourstoneCompanion-LocalTest-1.0.0.0-x64.msix` | Zusätzliches lokal signiertes MSIX mit separater Testidentität |
| `artifacts/msix/LocalTest/SHA256SUMS`, `package-validation.json`, `LocalTest.cer` | MSIX-Prüfsumme, genaue Prüfstufe und öffentliches Testzertifikat |
| `artifacts/msix/Store/HourstoneCompanion-Store-1.0.0.0-x64.msix` | Neuer Credits-Build mit bestätigter Partner-Center-Identität; SHA256 `01504e5e1ac7279ca09b6c6c33d03c672900810c812bc2ea7343f29b52d0c9c1` |
| `artifacts/store-submission/HourstoneCompanion-0.2.0-store-review.zip` und `.sha256` | Lokales Reviewbundle: Store-Dokumente, acht synthetische PNGs samt Checks, Einreichungsstatus und Datei-Prüfsummen; dieses ZIP wurde nicht übertragen |

Aktuelle App-DLL in Direktpublish, portablem Starttest und Produktions-MSIX:
`fd68149bce35004a28c7fbc90d1b61988f4d68e08f6e87cdb4fa0a6d7b0f4303`.
Vorheriger Kandidat ohne sichtbaren Credit, einschließlich nativer LocalTest-Prüfung:
`581876d9a407bf8768c1a32aeb37a21da214847e013479463b571a1ad5ce2ebd`.
Installierte, erfolgreich gestartete App-DLL vor der letzten Layoutkorrektur:
`c8ce80e8a8a7019a0b2c9fd76e3f46e575cc7aebf26ab96118e28decd44ba5cb`.
Die jeweiligen vollständigen Paketprüfsummen stehen in den genannten Manifesten.
Das vorherige Produktions-MSIX (`fee7f8a0ad41ba74686d537298033de45b2ffcddb5939059a1c0004cd0165db4`),
sein Validierungs-/Portalstatus sowie die vorherigen Release-Prüfsummen sind unter
`artifacts/archive/20260918-before-visible-credits` archiviert.

## Aktueller Credits-Kandidat

| Prüfung | Ergebnis / Evidenz |
|---|---|
| Direktpakete | `tools/package.ps1 -Unsigned` erfolgreich; neues Setup, Portable-ZIP und Updatepaket; `artifacts/releases/release-manifest.json` |
| Renderprüfungen des Direktbuilds | 19 bestanden; die frühere vollständige 125-Profil-Suite wird unten separat geführt |
| Aktueller portabler Start | Echter Rootlauncher, sichtbares Fenster, Dispatcher, isolierte SQLite-Datenbank und lokaler Abgleich bestanden; `artifacts/portable-smoke/startup-test-profile/smoke-result.json` |
| Aktuelles Produktions-MSIX | `tools/package-msix.ps1 -Profile Store` und MakeAppx bestanden; unveränderte Erstversion 1.0.0.0; `artifacts/msix/Store/package-validation.json` |
| Payloadgleichheit | Die neue App-DLL ist in Direktpublish, portablem Starttest und Store-Layout hashgleich; sie unterscheidet sich vom nativen LocalTest-Payload |
| Acht Storeaufnahmen | Mit dem neuen Credits-Payload erzeugt und alle Renderchecks bestanden; `artifacts/renders-blizzard-credit/final-store-render-manifest.json` dokumentiert den DLL-Hash und die Bildhashes |

Setup-Installation/Upgrade und native MSIX-Installation/Start wurden für diesen
Credits-Kandidaten nicht wiederholt. Der folgende Bestand trennt frühere Belege
ausdrücklich von dieser neuen Kandidatenprüfung.

## Frühere Prüfung und weiterhin relevante Belege

| Prüfung | Ergebnis / Evidenz |
|---|---|
| .NET-Core-Tests | 148 bestanden; `artifacts/test-results/core-implementation.trx` |
| WPF-/App-Tests | 184 bestanden (Debug), einschließlich Retail-Projektion, Einstellungen, Import und Launcher-Auflösung; `app-debug-implementation.trx` |
| Companion-Paket-/Privacy-Gates | 27 Python-Tests bestanden |
| Addon | 90 Lua-Szenarien und 38 Python-Tests bestanden |
| Gemeinsamer Vertrag | 13 gepinnte Dateien; neue Lua-/C#-Fixtures und Protokolldokumentation bytegleich |
| Interoperabilität | Tatsächliche C#-Datenaddon-Ausgabe mit dem Lua-Importer verarbeitet; fremde Daten bleiben aus dem lokalen Addon-Messcache ausgeschlossen |
| Vollständiger Daten-Rückweg | 38 Prüfungen über zwei isolierte Companion-Profile mit getrennten Ordnern und explizitem Dateitransport; zehn echte C#-Ausgaben im Lua-Importer bestanden, einschließlich des unveränderten Addon-Tags 0.3.1 und Addon 0.3.2; `artifacts/roundtrip-final/report.json` |
| WPF-Darstellung vor sichtbarem Credit | 125 Profile bestanden: Deutsch/Englisch, Hell/Dunkel/System, 100/150/200 % Render-Skalierung, kleine Fenster, lange Texte, Fortschritt, Details, Store-Einrichtung und bisherige Ansichten; kein neuer 125-Profil-Lauf für den Credits-Kandidaten behauptet |
| Visuelle Sichtprüfung | Fortschritt mit neun Slots und Details, Übernahme, Store-Einstellungen, bestehende Spielzeitansicht und kompakte Ansichten geprüft |
| Installiertes Upgrade | Original-Setup mit `--silent` ausgeführt; Installationshook und Setup erfolgreich; `artifacts/test-results/installer-upgrade.log` |
| Installierter Rootlauncher | `VelopackInstalled` erkannt, echtes Fenster und erfolgreicher Abgleich mit isoliertem Testprofil; `artifacts/runtime-tests/installed-start/smoke-result.json` |
| Vorhandene Einrichtung | Konsistentes SQLite-Backup vor dem Upgrade; Schema 1→2, Datenbankintegrität, Konfiguration, Quellen, Sichtbarkeit und Einstellungen erhalten; `artifacts/test-results/installed-upgrade.json` |
| Normaler App-Start | Vorhandenes Profil geladen; Fenster vorhanden, Prozess reagiert, `coreclr.dll` aus dem Installationsordner geladen, Autostart verweist auf vorhandenen Launcher; `artifacts/test-results/installed-runtime.json` |
| Lokales MSIX | Selbstständig lauffähiger Publish, MakeAppx-Manifestvalidierung, getrennte LocalTest-Identität, lokale Testsignierung und Hashgleichheit der App-DLL mit Direktpublish bestätigt |
| Nativer LocalTest-MSIX-Test vor sichtbarem Credit | Installation 1.0.0.0, zwei echte Paketstarts mit Medium Integrity (RID 8192), Update auf 1.0.1.0 bei unveränderten privaten Profil-Hashes, Deinstallation und unabhängige Zertifikatsbereinigung bestanden; [Details und Belege](MSIX-NATIVE-VALIDATION.md). Getestete App-DLL: `581876…`; weder der neue Credits-Payload noch die Produktionsidentität wurden damit installiert geprüft. |
| Produktions-Gates | Fehlende/ungültige Partner-Center-Identität oder Paketversion stoppt ausschließlich den Produktions-MSIX-Weg |
| Echtes lokales Addon-Update | Retail, Mists Classic und Classic Era mit `tools/update-local-addon.ps1` auf 0.3.2 aktualisiert; Backups geprüft, SavedVariables weder gelesen noch geändert; lokales JSON-Array unter `artifacts/addon/installed-local-2026-09-18.json`. Anniversary bleibt wegen laufendem WoW auf 0.3.1; separates Update offen. |
| Echtes Retail-Datenaddon | Retail-TOC bestätigt 0.3.2 und Protokoll 4; der normal laufende Companion hat `formatVersion = 4` in `Hourstone_Sync/Data.lua` erzeugt. Ein Laden per `/reload` im Spiel ist noch nicht geprüft. |

Der tatsächliche Installer-Test wurde nach ausdrücklicher Bestätigung und Beenden
der bisherigen App durchgeführt. Die Sicherung liegt außerhalb des Repositorys
unter `%LocalAppData%\Hourstone\Backups`. Die App-Daten wurden nicht in Pakete,
Screenshots oder öffentliche Repository-Dateien übernommen.

## Inhaltliche Abdeckung

- Fortschritt verwendet ausschließlich tatsächlich erfasste, sichtbare
  Retail-Charaktere. Suche, Realmfilter und Detailauswahl arbeiten auf derselben
  Datenmenge; ein Clientfilter und eine „Abgeglichen“-Anzeige fehlen bewusst.
  Unbekannte, bestätigte leere und veraltete Daten bleiben unterscheidbar.
- Fünf Familien werden unabhängig und deterministisch zusammengeführt. Neuere
  leere/niedrigere Werte, Reihen als Ganzes, Zeitstempelgleichstände, Wochenwechsel,
  Zukunftswerte, beschädigte Teilbereiche und ungültige Cloud-Snapshots sind geprüft.
- Legacy-Serialisierung/Hashes der Formate 1–3 bleiben erhalten. Zwei synthetische
  Companion-Profile tauschen Format 4 aus; verschiedene Addon-Versionen erhalten
  passend Format 3 oder 4. Zusätzliche Gerätebeiträge verdoppeln keine Spielzeit.
- Store-Übernahme prüft WAL-Inhalt, Migration, Quellen-IDs, neue Geräte-ID,
  Veröffentlichungsrevision, Konfiguration und ausgeblendete Charaktere. Fehler,
  Wiederholung, bestehendes Ziel, gesperrte Dateien, frische Einrichtung und
  Rollback sind mit synthetischen Profilen abgedeckt. Vor Abschluss der Einrichtung
  werden Service, Dateiwächter und WoW-Schreibzugriffe nicht gestartet.
- Die bisherigen 0.1.7-Änderungen bleiben enthalten, einschließlich Cloud-Datei-
  Diagnosen, Hinweisen zur lokalen Verfügbarkeit und Titelleisten-Fokuskorrektur.

## Noch nicht praktisch abgenommen

Der neue Rückwegtest lässt sich mit `tools/verify-progress-roundtrip.ps1`
wiederholen; Parameter und Abhängigkeiten stehen in
[`tools/verify-progress-roundtrip/README.md`](../tools/verify-progress-roundtrip/README.md).
Die erzeugten Profile, kopierten Cloud-Dateien und zehn Lua-Ausgaben bleiben als
synthetische Prüfdaten erhalten. Abgedeckt sind beide Übertragungsrichtungen,
alle fünf Fortschrittsbereiche, sinkender/entfernter Schlüsselstein, veraltete
Wochenstände, neue Nullwerte, beschädigte Peer-Snapshots, unbekannte lokale
Cacheversionen und das Ausbleiben einer erneuten Veröffentlichung fremder Daten.
Dies ist ein lokaler Integrationstest; die folgenden Grenzen bleiben bestehen.

- Der neue Credits-Installer wurde nicht gestartet. Der vorherige Setup-Kandidat
  wurde wie oben beschrieben durch Windows Code Integrity blockiert. Auch die damalige Release-Ausführung der App-Test-DLL
  wurde dort mit `0x800711C7` blockiert; die vollständige Debug-Suite bestand.
- Kein vollständig frisches Windows-System ohne vorinstalliertes .NET/SDK stand
  zur Verfügung. Die Pakete enthalten die Laufzeit, und die tatsächlich laufende
  App lädt nachweislich ihre lokale `coreclr.dll`. Ein Store-Konto oder eine
  Store-Anmeldung war für Direktbuild und Direktstart nicht erforderlich.
- Kein reales Paar physischer PCs mit WoW-Login und Dropbox/OneDrive/Proton-Drive-
  Transport. Der deterministische Zwei-Profil-Abgleich und echte C#→Lua-Import
  sind getestet; Providertransport und tatsächliche WoW-API-Daten bleiben separat.
- Das getrennte LocalTest-MSIX bestand den nativen Installations-/Start-/Update-/
  Deinstallationstest mit dem früheren App-Payload vor sichtbarem Credit. Der neue
  Credits-Kandidat und die Produktionsidentität wurden dadurch nicht installiert geprüft.
  Die Einreichung ist erfolgt; das Zertifizierungsergebnis steht aus. StartupTask,
  Registry-Ausnahme für den alten Autostart und geführte Migration sind weiterhin
  mit installiertem Paket zu prüfen. Die isolierten Starttests umgehen bewusst
  diese Einrichtungsaktionen. WACK ist als Microsoft-signierter Offline-Download
  [vorbereitet](WACK-PREPARATION.md), aber nicht installiert oder ausgeführt. Dieser
  eingestellte, optionale Zusatzcheck wird übersprungen und ist keine Einreichungsvoraussetzung.
- Kein Windows-Neustart zum interaktiven Tray-/Autostart-Test und kein tatsächlicher
  Monitorwechsel zwischen verschiedenen DPI-Einstellungen. Render-Skalierung ist
  kein Ersatz für diese Hardware-/Sitzungsprüfungen.
- Keine Installation eines veröffentlichten GitHub-Updates über den Updater.
  Der bestehende Feed-/Updateweg und seine Sicherheitsregeln bleiben erhalten.

## Store-Vorbereitung und Einführung

[Store-Technik](STORE.md), [deutsche Beschreibung](store/description.de.md),
[englische Beschreibung](store/description.en.md), [Datenschutzhinweise](store/privacy.md),
[Prüferanleitung](store/reviewer-notes.md) und [Einreichungscheckliste](store/submission-checklist.md)
sind vorbereitet. Acht WPF-Screenshots mit ausschließlich synthetischen Daten
liegen unter `artifacts/store-submission/screenshots` (DE/Dunkel und EN/Hell).

Produktionsidentität und Publisher wurden aus dem reservierten Partner-Center-
Produkt übernommen: Store-ID `9N9J1P7PKQTJ`, Paketname
`MartinKrebsSoftware.HourstoneCompanion`, Publisher
`CN=14D320E3-ECEA-427C-B053-C559BFCB7342`, Anzeigename **Martin Krebs Software**
und Paketversion `1.0.0.0`. Das erzeugte MSIX besteht MakeAppx und enthält dieselbe
aktuelle App-DLL wie die Direktversion. In Einreichung `1152921505701922590` wurde
es vor dem Absenden als **Validated / Packages Complete** bestätigt. Die Warnung zur
erforderlichen Freigabe von `unvirtualizedResources` bleibt sichtbar; Microsoft
entscheidet darüber während der Zertifizierung. Die Einreichung ist
über [Partner Center](https://partner.microsoft.com/en-us/dashboard/products/9N9J1P7PKQTJ/overview)
erreichbar und zeigt **In certification**; ein positives Zertifizierungsergebnis
oder eine Veröffentlichung liegt noch nicht vor.

**Properties** ist **Complete**: Kategorie **Utilities + tools**, Verarbeitung
personenbezogener Daten **Yes**, Projekt- und Support-URL sind gespeichert. Die
vollständigen deutschen und englischen Datenschutzhinweise sind mit dem bestätigten
Publishernamen als **Privacy policy text** hinterlegt. Diese offizielle Option
benötigt keine zusätzlich gehostete Datenschutz-URL. Die abschließende Inhalts-/
Kontaktprüfung, die Klärung der Blizzard-Asset-Nutzung und die offenen nativen
Store-Prüfungen bleiben erforderlich. Der lokale Statusbeleg liegt unter
`artifacts/store-submission/partner-center-status.json`. Die ursprünglichen
Blizzard-Icons wurden nicht ersetzt. Das lokale Test-MSIX ist keine Store-Freigabe.

Die deutschen und englischen Store-Beschreibungen einschließlich Kurztext,
Funktionen, Entwicklername und gekürztem Rechtehinweis sind gespeichert. Jede
Sprache enthält vier persistierte synthetische WPF-Screenshots; **Store listings**
ist **Complete**. Die Rechtehinweise halten die Grenze von 200 Zeichen ein.
**Submission options** ist ebenfalls **Complete**. Automatische Veröffentlichung
nach erfolgreicher Zertifizierung ist gespeichert und nach erneutem Laden bestätigt.
Die Pflichtbegründung für `unvirtualizedResources` ist innerhalb der Grenze von
500 Zeichen hinterlegt; Microsoft prüft die Fähigkeit im Zertifizierungsverfahren,
eine gesonderte Vorabgenehmigung ist für diese Einreichung nicht erforderlich.
Unter **Additional Testing Information** ist eine kompakte englische Fassung der
Prüferanleitung mit 4.886 Zeichen gespeichert und nach erneutem Laden bestätigt.
Sie enthält das synthetische Beispiel, den sichtbaren Credit, die Abgrenzung zum
historischen nativen Test, optionales/übersprungenes WACK sowie offene Tests und Assetrechte;
Zugangsdaten wurden nicht eingetragen. Die lokale Prüferanleitung ist ausführlicher.

Auch **Pricing and availability** und **Age ratings** sind **Complete**. Gespeichert
sind kostenlos (0 EUR, Referenzmarkt Deutschland), öffentliche Auffindbarkeit und
die unveränderte Portalvorgabe **All worldwide markets** mit 240 Märkten samt
künftigen Märkten. Diese Einstellungen gehören zur eingereichten Fassung;
eine öffentliche Verfügbarkeit besteht noch nicht.
Der IARC-Fragebogen wurde mit **All Other App Types**, neun Inhaltsantworten **No**
und **Physical media: No** nach ausdrücklich bestätigter Einwilligung gespeichert.
Die Vorschau zeigte USK Everyone / PEGI 3 / ESRB Everyone; die gespeicherte
Zusammenfassung nennt **Current Rating ID: Pending**. Es wird keine endgültig
erteilte Altersbewertung behauptet. **Submit for certification** wurde am
18.09.2026 um 15:01 UTC betätigt. Die automatische Veröffentlichung nach
erfolgreicher Zertifizierung ist durch den Portaltext bestätigt. Beim Absenden
erschienen kein neuer Vertrag und keine zusätzliche Zustimmungscheckbox.

Die [Asset-Prüfung](store/asset-review.md) inventarisiert die Dateien und vergleicht
die offiziellen Lizenz-/Store-Regeln. Die Herkunft der 17 Blizzard-Icons ist
belegt; eine konkrete Erlaubnis für diese App-Bündelung und Store-Abbildung ist
nicht nachgewiesen. Die MIT-Nutzung des Hourstone-Logos und seiner abgeleiteten
App-/Store-Icons ist durch die ausdrückliche Freigabe des Projektinhabers vom
18.09.2026 bestätigt und kein offener Logo-Freigabepunkt. Eine Anfrage zu den
Blizzard-Icons ist vorbereitet, wurde aber nicht versendet.

Der Projektinhaber hat ausdrücklich entschieden, die unveränderten Blizzard-Icons
mit Blizzard-Nennung in den Credits beizubehalten und trotz offener Nachweislage
zu veröffentlichen. Der Hinweis, dass Credits keine Erlaubnis ersetzen und eine
Store-Zulassung keine Rechteklärung ist, wurde gegeben. Diese Entscheidung ist
eine Freigabe zum Fortfahren, kein neuer Lizenznachweis.

Für Fortschrittsabgleich alle beteiligten Companions gemeinsam auf **0.2.0**
aktualisieren. Addon **0.3.1** bleibt als lokale Quelle lesbar; für den Rückweg
ins Spiel ist **0.3.2** erforderlich. Das Gruppenformat bleibt unverändert.
Das noch offene Anniversary-Update betrifft nur den lokalen Spielclient und ist
keine Voraussetzung der Store-Einreichung. Die gezielten Übernahme-/Autostarttests
und die ungeklärten Blizzard-Nutzungsrechte bleiben relevant.
