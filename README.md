# Fish

WinUI-3-Angelbot für World of Warcraft auf .NET 10.

## Start

```powershell
dotnet build Fish.csproj
dotnet run --project Fish.csproj
```

WoW wird automatisch anhand seines Fensters gefunden und muss sichtbar sein. Angeln liegt auf Taste **1**. Der Statuspixel muss ganz oben links in der WoW-Spielfläche (ohne Titelleiste und Rahmen) angezeigt werden.

Während einer Sitzung sind Einstellungen gesperrt. **Stop** bricht den Bot und ausstehende Countdowns ab.

## Oberfläche

Kompakte Oberfläche mit System-, Dark- und Light-Theme, den Bereichen Übersicht, Erkennung, Aktionen und Anwendung sowie aufklappbaren Einstellungsgruppen. Status und Aktivitätsprotokoll ersetzen die Bildvorschauen vollständig. WinUI-Übergänge und eine animierte Sitzungsanzeige zeigen Wechsel und laufende Sitzungen an. Neue Fenster starten mit 760 × 740 Pixeln; gespeicherte Fenstergrößen bleiben erhalten.

Das Protokoll hält die neuesten 300 Einträge mit Ereigniszeit, Kategorie und Stufe (Info, OK, Warnung, Fehler). Es dokumentiert Startkonfiguration, Fundpunkte, Auto-Learn, Folgeaktionen und eine Sitzungsbilanz. Loot-Anfragen und WeakAura-Loot-Bestätigungen werden getrennt gezählt; das ist keine Anzahl gefangener Fische. **Kopieren** übernimmt den gefilterten Verlauf chronologisch mit Datum und Millisekunden in die Zwischenablage; **Leeren** entfernt die angezeigten Einträge. Es wird kein dauerhaftes Sitzungslog geschrieben; Diagnoseberichte können den aktuellen Verlauf enthalten. Fensterbewegungen werden höchstens alle drei Sekunden protokolliert.

## Anwendung

Der Reiter **Anwendung** enthält Tray-, Tastatur-, Benachrichtigungs-, Update- und Diagnosefunktionen.

- Änderungen an Einstellungen werden nach 400 ms Eingabepause gespeichert, Fensterposition und normale Fenstergröße ebenfalls. Gelernte Toleranzen werden während der Sitzung und beim Stop übernommen. Fehler werden sichtbar gemeldet; eine nicht lesbare Einstellungsdatei wird nicht überschrieben.
- Minimieren blendet Fish standardmäßig in den Windows-Tray aus. Schließen beendet weiterhin die Anwendung, sofern „Beim Schließen in den Tray“ nicht aktiviert wurde. Das Tray-Menü bietet Öffnen, Starten, Stoppen und Beenden. Bei fehlendem Tray-Symbol bleibt das Fenster erreichbar.
- **F5** startet, **Umschalt+F5** stoppt, **Strg+1–4** wechselt Seiten, **Strg+Umschalt+C** kopiert das Log, **Strg+M** blendet Fish in den Tray aus und **Strg+Q** beendet die Anwendung. **Strg+Umschalt+F12** stoppt global; Konflikte mit anderen Anwendungen werden angezeigt. Globale Registrierung ist abschaltbar.
- Windows-Tray-Benachrichtigungen für Fehler, Folgeaktionen und Sitzungsende sind getrennt schaltbar, der Ton ebenfalls. Wiederholte Meldungen derselben Kategorie werden für zehn Sekunden begrenzt. Die Anzeige hängt von Windows-Benachrichtigungseinstellungen ab.
- Die manuelle Updateprüfung fragt die neueste stabile GitHub-Veröffentlichung von `leafcreek/Fish` ab. Tags müssen beispielsweise `v1.2.3` heißen. Version und Release-Notizen werden angezeigt; der Download-Link öffnet die GitHub-Release-Seite. Es gibt keinen automatischen Installer oder Hintergrunddownload. Die Produktversion stammt aus der Assembly-Version; für neue Releases muss sie im Projekt passend zum Tag gesetzt werden.
- Diagnoseberichte werden lokal unter `diagnostics` neben den Einstellungen abgelegt. Fehler erzeugen automatisch einen Bericht; ein manueller Export enthält System-/Versionsdaten, Sitzungszahlen, letzten Fehler und das aktuelle Protokoll. Unbehandelte Ausnahmen werden nach Möglichkeit mit Stacktrace protokolliert. Der Benutzerprofilpfad wird maskiert. Es werden höchstens 20 Berichte aufbewahrt und keine Berichte automatisch hochgeladen.

Die Tray-Anbindung nutzt [Shell_NotifyIcon](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw) und Windows-Hotkeys ohne zusätzliche Bibliotheken.

## Darstellung, Bedienung und erster Start

- **System / Dark / Light**: unter Anwendung umschaltbar, ohne Neustart. System folgt dem Windows-App-Theme. Standard ist System. Kontrastdesigns nutzen Systemfarben; die Graphen unterscheiden Kurven zusätzlich durch durchgezogene und gestrichelte Linien.
- **Barrierefreiheit**: Windows-Textskalierung, beschriftete Eingaben und Kennzahlen für Screenreader, Statusänderungen als höfliche Live-Region, Schriftgrößen ab 11 und eine Option „Bewegung reduzieren“. Die Windows-Einstellung für Animationen wird ebenfalls berücksichtigt. Kennzahlen bleiben als Text verfügbar.
- **Logsuche**: Strg+F fokussiert die Suche; mehrere Suchwörter werden kombiniert. Durchsucht werden Nachricht, Kategorie, Ereigniskennung und Stufe. Filter für einzelne Stufen, Warnungen+Fehler und die letzten 5/15/60 Minuten lassen sich kombinieren. Escape im Suchfeld leert den Suchtext; der Reset-Button setzt alle Filter zurück. Kopieren übernimmt nur Treffer, Diagnoseberichte weiterhin den vollständigen verfügbaren Verlauf. Statistikzähler bleiben unabhängig.
- **Fensterverwaltung**: Vordergrundstatus optional, Zentrieren auf dem aktuellen Monitor, Wiederherstellung von normaler Größe und Maximierungszustand. Die gespeicherte DPI wird beim Wiederherstellen berücksichtigt. Nicht mehr sichtbare Fenster werden auf einen verfügbaren Monitor verschoben; Taskleisten-Arbeitsflächen werden beachtet. Windows selbst übernimmt das Skalieren bei laufenden DPI-Wechseln.
- **Einführung**: beim ersten Start drei kurze Schritte, jederzeit überspringbar. Abschluss/Überspringen wird gespeichert. Erneut über F1 oder „Einführung anzeigen“ erreichbar; startet keine Sitzung.

`Tests/ExperienceSmoke.ps1` prüft Einführung, Theme-Auswahl, reduzierte Bewegung, Vordergrundstatus und Logsuche über Windows UI Automation. Kontrastdesigns, Narrator-Ausgabe und gemischte DPI-Monitore benötigen zusätzlich eine Prüfung am echten Desktop.

## Performance-Monitor

Unter dem Protokoll stehen native WinUI-Live-Graphen ohne zusätzliche Bibliotheken. Aktualisierung: einmal pro Sekunde; Verlauf: 60 Sekunden.

- **Loot/min und Würfe/min:** Ereignisse der letzten 60 Sekunden. In der ersten Minute wird die bisherige Sitzungsdauer hochgerechnet. Loot zählt WeakAura-Bestätigungen, keine Fisch-Stückzahlen.
- **Erfolgreiche Würfe:** eigene Würfe mit mindestens einer Loot-Bestätigung geteilt durch alle angeforderten Würfe, einschließlich des laufenden Wurfs. Ein beim Start bereits laufender Wurf zählt nicht als eigener Erfolg.
- **Ø Wurf → Loot:** Zeit vom eigenen Wurf bis zu seiner ersten Bestätigung, gemittelt über erfolgreiche Würfe.
- **Ø Scan:** Aufnahme, Fensterprüfung und Bobber-/Splash-Suche, gemittelt über die letzten fünf Sekunden; ohne Scan-Warteintervall und WeakAura-Pixelaufnahme. Ohne aktuelle Messung erscheint ein Strich.
- **CPU/RAM:** nur Fish; CPU ist auf alle logischen Prozessoren normiert, RAM entspricht dem Working Set.

Gesamtzahlen, Timeouts und Sitzungsdauer bleiben nach Stop erhalten. Graphen frieren ein; CPU/RAM bleiben live. Ein neuer Start setzt die Sitzungswerte zurück. Das Leeren des Logs beeinflusst die Statistik nicht.

## Einstellungen

Fish speichert Änderungen automatisch nach 400 ms Eingabepause und zusätzlich beim Beenden in `appsettings.json` neben der EXE und lädt sie beim nächsten Start wieder. Dazu gehören Fensterposition und -größe, Steuerung, Erkennungswerte einschließlich gelernter Toleranzen sowie Folgeaktionen. Beim Speichern entsteht eine `.bak`-Datei. Der EXE-Ordner muss beschreibbar sein; bei einem Schreibfehler erscheint eine Meldung. Bisherige Einstellungen unter `%APPDATA%` werden nicht mehr geladen.

| Eigenschaft | Standardwert |
| --- | --- |
| Übersprungene Pixel | 2 |
| Bobber-Breite | 800 px |
| Bobber-Höhe | 400 px |
| Bobber-Y-Versatz nach oben | 30 px |
| Bobber-Farbtoleranz | 200 |
| Bobber automatisch lernen | Ein |
| Splash-Farbtoleranz | 70 |
| Splash automatisch lernen | Ein |

Defaults gelten für neue Einstellungen und fehlende JSON-Felder. Bereits gespeicherte Werte haben Vorrang.

## Verhalten und Aufbau

Die UI-unabhängige Engine in `Core` verarbeitet WeakAura-Zustände, Bobber-/Splash-Erkennung, Auto-Learn, Cast, Loot und Timeout. `Services` enthält die Desktop-Anbindung, `Native` die Win32-Aufrufe und durchklickbaren Overlays. `Configuration` verwaltet die Einstellungen; `Views` und `ViewModels` die Oberfläche.

WoW darf grundsätzlich inaktiv sein, muss aber weiterhin sichtbar sein. Fish bleibt bei aktivierter Option „Immer im Vordergrund“ über normalen Fenstern, ohne den Tastaturfokus zu übernehmen. Verdecktes oder minimiertes WoW wird nicht zuverlässig erfasst. Suchbereiche beziehen sich auf die Mitte der WoW-Spielfläche und folgen dem Fenster beim Verschieben und bei Größenänderungen. Dabei werden alte Fundkoordinaten verworfen und der Bobber erneut gesucht. Minimieren von WoW pausiert Aufnahme und Eingaben und blendet die Bereiche aus. Die Erkennung verwendet immer die rote Feder. Beide Suchbereiche sind auch vor dem Start und nach Stop sichtbar; Änderungen an ihrer Größe und Position werden laufend angezeigt. Ohne erkannten Bobber zeigt der Splash-Rahmen eine Größenvorschau relativ zur Mitte des Bobber-Suchbereichs. Minimieren von Fish blendet die Rahmen nicht aus.

Inaktivität bedeutet 120 Sekunden ohne Loot. Optionale Folgeaktionen gelten nur während einer gestarteten Sitzung: WoW nach 60 Sekunden schließen, Windows nach 120 Sekunden herunterfahren. Beide Countdowns beginnen beim selben Ereignis; Stop bricht sie ab.

## Prüfungen und Veröffentlichung

```powershell
dotnet run --project Tests/Fish.Tests.csproj
dotnet run --project Tests/Fish.Tests.csproj -- --desktop-smoke
```

Die Tests prüfen Erkennung, Defaults, Speicherung, erneutes Laden, simulierte Engine-Abläufe, Performance-Zähler, Release-Validierung und Diagnoseberichte. `Tests/ApplicationSmoke.ps1` ergänzt den lokalen UI-Test für Autosave, Tray-Minimierung/Wiederherstellung, Diagnoseexport und Updateprüfung. In Umgebungen ohne zugänglichen Windows-Tray kann der UI-Test mit `-SkipTray` ausgeführt werden; das ersetzt keine Prüfung des Tray-Verhaltens auf dem Desktop. Der Desktop-Smoke-Test ergänzt Monitor-Erkennung, eine 2×2-Pixel-Aufnahme einen kurz sichtbaren Overlay-Rahmen sowie Client-Koordinaten eines eigenen Testfensters. Testdaten liegen unter `artifacts/tests`.

Isolierter App-Test:

```powershell
dotnet run --project Fish.csproj -- --settings-directory E:\Dev\Fish\artifacts\smoke-settings
```

Veröffentlichung für x64:

```powershell
dotnet publish Fish.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -o artifacts/publish
```

Erkennungsqualität, WeakAura-Kompatibilität, Spieleingaben und gemischte DPI-Skalierung müssen zusätzlich in einer echten Spielsitzung geprüft werden.

Die Anwendung nutzt normale Windows-Fenster- und Bildschirm-APIs. Es gibt keine Prozessspeicherzugriffe, Injection, Hooks oder Treiber. Das ist keine Garantie für Nicht-Erkennung durch Anti-Cheat. Die Aufnahme erfasst die sichtbaren Bildschirmpixel innerhalb der Spielfläche; überdeckende Fenster können weiterhin die Erkennung beeinflussen.
