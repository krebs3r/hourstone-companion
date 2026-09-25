# Zertifizierungsfix — 24. September 2026

Das Store-Paket **1.0.1.0** (Produktversion **0.2.0**) wurde am **25. September 2026 um 19:36 UTC / 21:36 Europe/Berlin erneut zur Zertifizierung eingereicht**. Das Partner Center bestätigte „Zertifizierung wird ausgeführt“: Übermittlung abgeschlossen, Vorverarbeitung läuft, Zertifizierung und Veröffentlichung noch nicht gestartet. Der Zeitpunkt bezeichnet die beobachtete Minute, keinen Server-Zeitstempel. Eine Freigabe liegt noch nicht vor.

## Anlass und Änderungen

Der Zertifizierungsbericht zur Einreichung vom 18. September nennt am **21. September 2026** einen Fehler unter **10.6.3 Capabilities** sowie einen fehlenden geeigneten Supportkontakt. Der konkrete Berechtigungsname steht dort nur als Platzhalter `<enter RC>`. Dass `unvirtualizedResources` gemeint ist, bleibt eine aus Manifest und Einreichungsunterlagen abgeleitete Annahme.

- `unvirtualizedResources`, Registry-Ausnahme und XML-Namensraum entfernt. `runFullTrust` und optionaler Store-StartupTask bleiben erhalten.
- Die Store-Übernahme schreibt oder restauriert keinen alten Autostarteintrag. Bei Altprofil, altem Eintrag oder fehlgeschlagener Erkennung erscheint eine deutsche/englische Wechselanleitung mit Windows-Einstellungslink und Bestätigung. Ein verbleibender Registry-Wert blockiert den Wechsel nicht.
- SQLite-Backup, neue Gerätekennung, Schutz abgeschlossener Profile und Instanzsperre bleiben erhalten. Fehler rollen nur das Store-Profil und gegebenenfalls dessen StartupTask zurück.
- Credits: Consolas 13, Überschrift 24 SemiBold, feste Zeilenumbrüche nach beiden Nennungen von „Blizzard Entertainment, Inc.“.
- Lokale Diagnose unter Synchronisierung hinter der Geräteliste; weiterhin markierbar, kopierbar, schreibgeschützt und auch ohne Cloud-Ordner erreichbar. Hinweise und gezielte Diagnosevorschauen zeigen auf die neue Position; normale Seitenaufrufe beginnen oben.
- Datenbankformat und Synchronisierungsprotokoll unverändert. Änderungen gelten auch für die direkte Ausgabe, soweit distributionsübergreifend.

## Paketbindung

Produktionspaket: `artifacts/msix/Store/HourstoneCompanion-Store-1.0.1.0-x64.msix`

SHA256: `08acf2c4dc09e25258dc4fa2b9ab07502c71a730657eb03ee553db5fd6699cef`

Anwendungs-DLL in Store- und LocalTest-Paket: `4f78d3edeae9f6c70db30a64e605c21f20d48177f598506a0d1c6290d338be3b`

Das Produktionspaket ist für die vorhandene Partner-Center-Identität gebaut und lokal nicht signiert/installiert. Der native Test verwendet eine **separate LocalTest-Identität mit derselben Anwendungs-DLL**. Er ist kein Installationstest der Produktionsidentität.

Vorherige Pakete, Einreichungsunterlagen und Arbeitsdiff bleiben unter `artifacts/archive/20260924-before-certification-fix/` erhalten. Bereits vorhandene Arbeitsänderungen wurden beibehalten.

## Bestätigte Prüfungen

| Prüfung | Ergebnis / Nachweis |
| --- | --- |
| Release-Build | Erfolgreich, keine Warnungen/Fehler |
| .NET-Tests | **193 App + 148 Core = 341 bestanden**; `artifacts/certification-fix/test-results/` |
| Python-Prüfungen | **40 bestanden**; `artifacts/certification-fix/python-tests-unsandboxed.log` |
| Datenschutz-, Protokoll- und Asset-Prüfung | Bestanden; Protokoll 4 unverändert |
| Erweiterte WPF-Renderprüfung | **125 bestanden**; `artifacts/certification-fix/render.log` und `renders/` |
| Direkte Pakete | Neu gebaut; 19 Paket-/Portable-Renderprüfungen und isolierter tatsächlicher Portable-Start bestanden; `direct-package.log` |
| Store-Paket | MakeAppx-Validierung bestanden; `store-package.log` und `artifacts/msix/Store/package-validation.json` |
| Einreichungsaufnahmen | **14 Aufnahmen**, jeweils mit bestandener `.checks.json`; DE/dunkel, EN/hell unter `artifacts/store-submission/screenshots/` |
| Native LocalTest-Installation | Installation, normaler Benutzerstart, Update 1.0.0.0 → 1.0.1.0, Erhalt isolierter Testdaten, erneuter Start und Deinstallation bestanden |
| Laufende Altanwendung | Installierter Einrichtungsdialog bleibt auch nach „Erneut prüfen“ gesperrt; Wechselanleitung sichtbar. Keine echte Datenübernahme ausgeführt |
| Aufräumen | Testpakete, Testprofile und temporäres Zertifikatsvertrauen unabhängig als entfernt bestätigt; `independent-cleanup.json` |

Die Import-/Neueinrichtungstests prüfen unter anderem unveränderte Quelldaten, neue Geräte-ID, Schutz abgeschlossener Profile, Wiederholung nach Fehlern, verweigerte StartupTask-Aktivierung, Rollback, Abbruch und manuelle Wechselbestätigung. StartupTask-Tests verwenden eine injizierbare Schnittstelle; sie ersetzen keinen Windows-Anmeldetest.

Renderprüfungen umfassen Deutsch/Englisch, hell/dunkel, kleine Fenster und Diagnosezustände ohne Quellen, mit erfolgreichem Sync und mit nicht lokal verfügbarer Cloud-Datei. Credits, Wechselanleitung und Diagnose wurden zusätzlich visuell geprüft. Die Einreichungsaufnahmen verwenden synthetische Daten.

Native Belege liegen unter `artifacts/msix-validation/prepared/20260924-172149-0409084ebb374a0385ff1b453b2bb22d/`:

- Plan-SHA256: `9f1f664d681c700d846b70c86139c5d968efe634e0cef329a883b2e8a6c885a2`.
- Testpaket 1.0.0.0: `7cc74726b370667f96129c33805075cdfd31fd33d90dcd10690d8f060f55719a`.
- Testpaket 1.0.1.0: `3e0a9f35d42a3a20a795dde0532cf8cd99712a3acab9fab7587ce05c162cbb78`.
- Ergebnis: `results/20260924-172321-4d011742b2c7434f8d786be1f7200c82/native-test-result.json`; beide gestarteten Prozesse Medium Integrity, RID 8192; alter Autostart unverändert.
- Temporäres Zertifikat `9787468E93FA8EF4D5FE4663AC39072592EBCFDB` ausschließlich kurzzeitig unter TrustedPeople, anschließend entfernt. Unabhängige Abschlussprüfung: 24. September, 17:29:33 UTC.

## Partner Center und erneute Einreichung am 25. September

In den Produkteigenschaften der Einreichung `1152921505701922590` ist **mail@martin-krebs.eu** als Supportkontakt gespeichert und nach erneutem Öffnen bestätigt. Die Website bleibt **https://github.com/krebs3r/hourstone-companion**. Projekt und Issues-Seite lieferten ohne Anmeldung HTTP 200; Nachweis: `artifacts/certification-fix/public-support-check.json`.

Aktuelle lokale Unterlagen: [Reviewer notes](reviewer-notes.md), [Checkliste](submission-checklist.md), aktualisierte Datenschutzbeschreibung und Screenshots. Der kompakte Begleittext `artifacts/store-submission/resubmission-notes-1.0.1.0.txt` bleibt als früherer Entwurf erhalten. Die tatsächlich gespeicherten und vor Übermittlung erneut gelesenen 4.820 Zeichen stehen in `artifacts/store-submission/reviewer-notes-submitted-20260925.txt`. Das aktuelle Prüfbündel liegt unter `artifacts/store-submission/review-bundle-1.0.1.0/`.

Das Portal enthielt vor Übermittlung ausschließlich das Paket 1.0.1.0 mit Status **Validated**. Beide Sprachgalerien sind mit jeweils sieben aktuellen synthetischen Bildern gespeichert und visuell kontrolliert. Die deutsch/englische Datenschutzbeschreibung wurde um den Supportkontakt ergänzt, gespeichert und erneut geöffnet. Die Reviewer-Notizen erläutern die entfernte Berechtigung, den manuellen Wechsel und die tatsächlichen Testgrenzen. Der alte Berechtigungswarnhinweis entfiel mit dem neuen Paket. Nach dem ausdrücklich beauftragten Klick auf „Erneut zur Zertifizierung übermitteln“ wechselte das Portal in den laufenden Zertifizierungsprozess. Die automatische Veröffentlichung nach erfolgreicher Zertifizierung bleibt eingestellt. Nachweis: `artifacts/store-submission/resubmission-confirmation-20260925.json`; aktueller Status: `artifacts/store-submission/partner-center-status.json`. Vorherige Unterlagen bleiben in den Archiven vom 24. und 25. September erhalten.

## Bewusst offene Prüfungen

Die vorhandene Windows-Sitzung enthält eine laufende produktive Direktinstallation. Eine getrennte Windows-Testumgebung steht nicht bereit. Daher bleiben vollständiger installierter Einrichtungs-/Übernahmeablauf, Tray-Bedienung nach Einrichtung, tatsächlicher Autostart nach Windows-Anmeldung und externe Dateiausgabe aus einer regulär eingerichteten Store-Installation offen. Der native Smoke-Test nutzt ein isoliertes Profil und umgeht den normalen Einrichtungsablauf. Reale Cloud-Anbieter, mehrere Windows-Sitzungen und natives WoW wurden damit nicht geprüft.

Der Nutzer hat am 24. September die vollständige Datenübernahme als weniger wichtig eingeordnet, weil Charakterdaten erneut eingelesen werden können. Eine Neueinrichtung kann vorhandene Addon-Dateien nach Auswahl der WoW-Ordner wieder einlesen. Neu erfasste Werte werden nach Ausloggen oder `/reload` gespeichert. App-Einstellungen, Ordnerauswahl, Cloud-Verbindung und ausschließlich im alten Profil vorhandene Daten werden damit nicht automatisch wiederhergestellt. Die optionale Übernahme bleibt verfügbar; der fehlende vollständige Praxistest wird nicht als bestanden ausgewiesen.

WACK wurde nicht ausgeführt. Historische Nachweise und die bereits dokumentierte Entscheidung zu unveränderten Spielgrafiken bleiben erhalten. Die separat beauftragte erneute Übermittlung erfolgte am 25. September; die nachstehenden Testgrenzen wurden dadurch nicht als bestanden gewertet.
