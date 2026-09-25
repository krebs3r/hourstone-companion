# Lokales Addon sicher aktualisieren

`tools/update-local-addon.ps1` prüft das lokal gebaute `artifacts/addon/Hourstone-0.3.2.zip` und ersetzt bei ausdrücklichem `-Apply` ausschließlich `Interface\AddOns\Hourstone` der angegebenen Clients. Voraussetzung: Windows und PowerShell 7.4 oder neuer. Das Skript benötigt keine zusätzlichen Bibliotheken.

Ohne `-Apply` erfolgt nur ein Trockenlauf. Es sucht keine Installationen und liest weder die Companion-Datenbank noch Accounts oder SavedVariables. Jeder Clientpfad muss ausdrücklich angegeben werden. Beispiel mit einem fiktiven Installationspfad; an die eigene Installation anpassen:

```powershell
$clients = @(
    'D:\Games\World of Warcraft\_anniversary_',
    'D:\Games\World of Warcraft\_classic_',
    'D:\Games\World of Warcraft\_classic_era_',
    'D:\Games\World of Warcraft\_retail_'
)
./tools/update-local-addon.ps1 -ClientDirectory $clients -Preflight
```

Der JSON-Bericht zeigt Version, Zielverzeichnis und `Ready`/`Blocked`. `CanApply` muss für alle ausgewählten Clients `true` sein; bei `false` verändert auch `-Apply` keinen davon. Ein blockierter Trockenlauf liefert weiterhin einen normalen JSON-Bericht, deshalb bei automatischer Auswertung `CanApply` prüfen. Nicht prüfbare WoW-Prozesspfade blockieren ebenso wie ein laufender Prozess aus dem jeweiligen Client. Ein bekanntes laufendes Spiel in einem anderen Client blockiert nur dessen eigenes Ziel.

Für die tatsächliche Aktualisierung die betroffenen Spiele vorher vollständig schließen und während des Updates geschlossen lassen:

```powershell
./tools/update-local-addon.ps1 -ClientDirectory $clients -Apply
```

Vor dem Austausch wird der vorhandene Addon-Ordner nach `%LOCALAPPDATA%\Hourstone\Backups\Addon\<UTC-Zeit>-<ID>\Hourstone` kopiert und anhand seiner Datei-Hashes geprüft. Das ZIP wird neben dem Ziel entpackt, geprüft und anschließend umbenannt. Bei Fehlern vor Abschluss wird der ursprüngliche Ordner wiederhergestellt, sofern dies ohne laufendes Spiel oder unsichere Pfade möglich ist. Andernfalls nennt die Fehlermeldung die erhaltenen Sicherungs-/Rollback-Pfade. Vorhandene Backups werden nicht gelöscht. Bei mehreren Clients bleibt eine bereits erfolgreich abgeschlossene Aktualisierung bestehen, falls ein späterer Client scheitert.

Das Skript prüft den fest hinterlegten SHA-256 des ZIPs, Addon-Version und Protokoll-4-Kennung. Unzulässige ZIP-Pfade, Links/Junctions, überlappende Ziel-/Backup-Pfade und ein Downgrade werden abgewiesen. `WTF`, SavedVariables und `Hourstone_Sync` werden weder gelesen noch geändert. `-ArchivePath`, `-ExpectedSha256` und `-ExpectedVersion` sind nur gemeinsam für ein bewusst geprüftes anderes Paket anzupassen.

## Durchgeführte Prüfung

Am 18.09.2026: zwölf synthetische Tests erfolgreich, einschließlich Sicherung, Rollback bei manipuliertem Staging, ZIP-Pfad- und Link-Angriffen, Prozesssperren und unverändertem SavedVariables-Kontrollinhalt. Tests verwenden ausschließlich temporäre künstliche Clients und einen separaten künstlichen Backup-Pfad:

```powershell
python -m unittest discover -s tools/verification -p test_update_local_addon.py -v
```

Der echte Trockenlauf unter `artifacts/addon/preflight-local-2026-09-18.json` zeigte viermal Version 0.3.1: Mists Classic, Classic Era und Retail `Ready`, Anniversary wegen laufendem WoW `Blocked`. Anschließend wurden **Mists Classic, Classic Era und Retail erfolgreich auf 0.3.2 aktualisiert**, jeweils mit geprüftem Backup. SavedVariables wurden weder gelesen noch geändert. Der lokale Ergebnisbericht `artifacts/addon/installed-local-2026-09-18.json` ist ein JSON-Array mit den drei erfolgreichen Aktualisierungen; er enthält lokale Installations-/Backup-Pfade, aber keine Account- oder Charakterdaten und gehört nicht in öffentliche Pakete.

Anniversary bleibt auf 0.3.1. Sein Update ist noch nicht ausgeführt; vor einer gesondert bestätigten Aktualisierung müssen das laufende Spiel beendet und der Trockenlauf erneut geprüft werden. Ein erfolgreiches Dateiupdate ersetzt keine Prüfung im Spiel.

Dieses Update betrifft nur den lokalen Anniversary-Client und ist keine Voraussetzung für die Store-Einreichung des Companion.

Zusätzlich bestätigt: Die echte Retail-TOC nennt Version 0.3.2 und `X-Hourstone-Sync-Protocol: 4`. Der normal laufende Companion hat im Retail-Datenaddon `Hourstone_Sync/Data.lua` tatsächlich `formatVersion = 4` erzeugt. Ein anschließendes Laden per `/reload` im Spiel wurde noch nicht geprüft.
