# Fish

WinUI-3-Angelbot für World of Warcraft auf .NET 10.

## Start

```powershell
dotnet build Fish.csproj
dotnet run --project Fish.csproj
```

WoW wird automatisch anhand seines Fensters gefunden und muss sichtbar sein. Angeln liegt auf Taste **1**. Der Statuspixel muss ganz oben links in der WoW-Spielfläche (ohne Titelleiste und Rahmen) angezeigt werden.

Das eigenständige WoW-Forever-Addon **FishHelper** liegt unter [`addon/FishHelper`](addon/FishHelper/README.md). Den Ordner nach `Interface\AddOns` der Forever-Installation kopieren und den Client neu starten. Es erzeugt die bestehenden Statusfarben ohne WeakAuras-Abhängigkeit. Installation, Diagnose und offene Prüfungen im Spiel stehen in der verlinkten Anleitung.

Während einer Sitzung sind Einstellungen gesperrt. **Stop** bricht den Bot und ausstehende Countdowns ab.

## Oberfläche

Kompakte Oberfläche mit Dark- und Light-Theme, den Tabs Übersicht, Erkennung, Aktionen und Anwendung sowie aufklappbaren Einstellungsgruppen. Status, Aktivitätsprotokoll und drei kompakte Bildvorschauen begleiten die Sitzung. WinUI-Übergänge und eine animierte Sitzungsanzeige zeigen Wechsel und laufende Sitzungen an. Neue Fenster starten mit 760 × 740 Pixeln; gespeicherte Fenstergrößen bleiben erhalten.

Das Protokoll hält die neuesten 300 Einträge mit Ereigniszeit, Kategorie und Stufe (Info, OK, Warnung, Fehler). Es dokumentiert Startkonfiguration, Fundpunkte, Bewegungserkennung, Folgeaktionen und eine Sitzungsbilanz. Loot-Anfragen und WeakAura-Loot-Bestätigungen werden getrennt gezählt; das ist keine Anzahl gefangener Fische. Es wird kein dauerhaftes Sitzungslog geschrieben. Fensterbewegungen werden höchstens alle drei Sekunden protokolliert.

## Anwendung

Der Reiter **Anwendung** enthält Darstellungs-, Tray-, Tastatur- und Updatefunktionen.

- Änderungen an Einstellungen werden nach 400 ms Eingabepause gespeichert, Fensterposition und normale Fenstergröße ebenfalls. Fehler werden sichtbar gemeldet; eine nicht lesbare Einstellungsdatei wird nicht überschrieben.
- Minimieren blendet Fish standardmäßig in den Windows-Tray aus. Schließen beendet weiterhin die Anwendung, sofern „Beim Schließen in den Tray“ nicht aktiviert wurde. Das Tray-Menü bietet Öffnen, Starten, Stoppen und Beenden. Bei fehlendem Tray-Symbol bleibt das Fenster erreichbar.
- Die manuelle Updateprüfung fragt die neueste stabile GitHub-Veröffentlichung von `leafcreek/Fish` ab. Tags müssen beispielsweise `v1.2.3` heißen. Das Ergebnis erscheint als Dialog; bei einer neuen Version öffnet dessen Download-Schaltfläche die GitHub-Release-Seite. Es gibt keinen automatischen Installer oder Hintergrunddownload. Die Produktversion stammt aus der Assembly-Version; für neue Releases muss sie im Projekt passend zum Tag gesetzt werden.

Die Tray-Anbindung nutzt [Shell_NotifyIcon](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw) ohne zusätzliche Bibliotheken.

## Darstellung und Bedienung

- **Dark / Light**: unter Anwendung umschaltbar, ohne Neustart. Standard ist Dark; bisher gespeicherte System-Einstellungen werden auf Dark umgestellt.
- **Fensterverwaltung**: Vordergrundstatus optional, Wiederherstellung von normaler Größe und Maximierungszustand. Die gespeicherte DPI wird beim Wiederherstellen berücksichtigt. Nicht mehr sichtbare Fenster werden auf einen verfügbaren Monitor verschoben; Taskleisten-Arbeitsflächen werden beachtet. Windows selbst übernimmt das Skalieren bei laufenden DPI-Wechseln.

Der UI-Test `Tests/ExperienceSmoke.ps1` prüft Themes, Vordergrundstatus und die drei einklappbaren Bereiche.

## Analyse

Unter dem Protokoll stehen native WinUI-Live-Graphen ohne zusätzliche Bibliotheken. Aktualisierung: einmal pro Sekunde; Verlauf: 60 Sekunden.

- **Loot/min und Würfe/min:** Ereignisse der letzten 60 Sekunden. In der ersten Minute wird die bisherige Sitzungsdauer hochgerechnet. Loot zählt WeakAura-Bestätigungen, keine Fisch-Stückzahlen.
- **Erfolgreiche Würfe:** eigene Würfe mit mindestens einer Loot-Bestätigung geteilt durch alle angeforderten Würfe, einschließlich des laufenden Wurfs. Ein beim Start bereits laufender Wurf zählt nicht als eigener Erfolg.
- **Ø Wurf → Loot:** Zeit vom eigenen Wurf bis zu seiner ersten Bestätigung, gemittelt über erfolgreiche Würfe.
- **Ø Scan:** Aufnahme, Fensterprüfung und Bobber-/Splash-Suche, gemittelt über die letzten fünf Sekunden; ohne Scan-Warteintervall und WeakAura-Pixelaufnahme. Ohne aktuelle Messung erscheint ein Strich.
- **CPU/RAM:** nur Fish; CPU ist auf alle logischen Prozessoren normiert, RAM entspricht dem Working Set.

Gesamtzahlen, Timeouts und Sitzungsdauer bleiben nach Stop erhalten. CPU/RAM-Graphen laufen weiter; ohne aktive Sitzung entstehen keine neuen Scanwerte. Ein neuer Start setzt die Sitzungswerte zurück. Die Statistik ist unabhängig vom Protokoll.

## Einstellungen

Fish speichert Änderungen automatisch nach 400 ms Eingabepause und zusätzlich beim Beenden in `appsettings.json` neben der EXE und lädt sie beim nächsten Start wieder. Dazu gehören Fensterposition und -größe, Steuerung, Erkennungswerte und Bewegungsgrenzen sowie Folgeaktionen. Beim Speichern entsteht eine `.bak`-Datei. Der EXE-Ordner muss beschreibbar sein; bei einem Schreibfehler erscheint eine Meldung. Bisherige Einstellungen unter `%APPDATA%` werden nicht mehr geladen.

| Eigenschaft | Standardwert |
| --- | --- |
| Bobber-Breite | 800 px |
| Bobber-Höhe | 400 px |
| Bobber-Y-Versatz nach oben | 30 px |
| Bobber-Mindestübereinstimmung | 80 % |
| Splash-Pixel-Rauschtoleranz | 20 |
| Splash-Mindeständerung | 5 % |
| Splash-Kalibrierung | 500 ms |
| Normale Wasserbewegung berücksichtigen | Ein |

Defaults gelten für neue Einstellungen und fehlende JSON-Felder. Bereits gespeicherte Werte haben Vorrang.

Die Bobber-Suche verwendet Template Matching mit der eingebetteten [Forever-Bildvorlage](Assets/Bobber/README.md) aus dem bereitgestellten transparenten PNG. Sie prüft Größen von 60 bis 140 Prozent, Drehungen von ±30°, Breitenfaktoren von 80 bis 120 Prozent und perspektivische Verformungen. Die Suche vergleicht die Helligkeitsstruktur unter Berücksichtigung der Alphamaske und der aus der Vorlage ermittelten Körperfarbe und wählt den besten Treffer oberhalb der **Mindestübereinstimmung** (einstellbar von 10 bis 99 %). Der Wert beschreibt die Ähnlichkeit, keine Trefferwahrscheinlichkeit. Größere Werte sind strenger. Die Bobber-Vorschau markiert Treffer grün und kennzeichnet den Anker im Schwimmerkörper; das Protokoll nennt Übereinstimmung, Größe, Drehung, Verformung und Fundpunkt. Der Körper-Anker wird automatisch aus der PNG-Maske bestimmt und bei allen Verformungen mitgeführt. Die Mindestübereinstimmung wird nicht automatisch gelernt. Alte Bobber-Farbtoleranz-/Auto-Learn-Werte werden ignoriert.

Die [Splash-Erkennung](docs/splash-motion.md) vergleicht jedes Bild mit dem vorherigen Rohbild. Rote Pixel in der Live-Vorschau markieren deutliche Änderungen; darunter stehen Änderungsanteil, wirksame Grenze und normale Wasserbewegung. Nach Kalibrierung und ausreichender zusammenhängender Bewegung wird genau eine Loot-Anfrage pro Wurf am erkannten Schwimmer gesendet. Die Wasser-Baseline wird bei jedem neuen Wurf oder Referenzwechsel neu ermittelt. Alte Splash-Farbtoleranz-/Auto-Learn-/Pixel-Schritt-Einstellungen werden ignoriert; die Geometrie bleibt erhalten.

## Verhalten und Aufbau

Die UI-unabhängige Engine in `Core` verarbeitet WeakAura-Zustände, Bobber-/Splash-Erkennung, Bewegungskalibrierung, Cast, Loot und Timeout. `Services` enthält die Desktop-Anbindung, `Native` die Win32-Aufrufe und durchklickbaren Overlays. `Configuration` verwaltet die Einstellungen; `Views` und `ViewModels` die Oberfläche.

WoW darf grundsätzlich inaktiv sein, muss aber weiterhin sichtbar sein. Fish bleibt bei aktivierter Option „Immer im Vordergrund“ über normalen Fenstern, ohne den Tastaturfokus zu übernehmen. Verdecktes oder minimiertes WoW wird nicht zuverlässig erfasst. Suchbereiche beziehen sich auf die Mitte der WoW-Spielfläche und folgen dem Fenster beim Verschieben und bei Größenänderungen. Dabei werden alte Fundkoordinaten verworfen und der Bobber erneut gesucht. Minimieren von WoW pausiert Aufnahme und Eingaben und blendet die Bereiche aus. Die Bobber-Erkennung verwendet die eingebettete Forever-Bildvorlage; die Bisserkennung vergleicht aufeinanderfolgende Bilder des Splash-Bereichs. Der Fundpunkt liegt jetzt im Schwimmerkörper, deshalb kann der bisherige Splash-Versatz eine Anpassung benötigen. Beide Suchbereiche sind auch vor dem Start und nach Stop sichtbar; Änderungen an ihrer Größe und Position werden laufend angezeigt. Ohne erkannten Bobber zeigt der Splash-Rahmen eine Größenvorschau relativ zur Mitte des Bobber-Suchbereichs. Minimieren von Fish blendet die Rahmen nicht aus.

Inaktivität bedeutet 120 Sekunden ohne Loot. Optionale Folgeaktionen gelten nur während einer gestarteten Sitzung: WoW nach 60 Sekunden schließen, Windows nach 120 Sekunden herunterfahren. Beide Countdowns beginnen beim selben Ereignis; Stop bricht sie ab.

## Prüfungen und Veröffentlichung

```powershell
dotnet run --project Tests/Fish.Tests.csproj
dotnet run --project Tests/Fish.Tests.csproj -- --desktop-smoke
```

Die Tests prüfen Erkennung, Defaults, Speicherung, erneutes Laden, simulierte Engine-Abläufe, Performance-Zähler, Release-Validierung und Einstellungen. `Tests/ApplicationSmoke.ps1` ergänzt den lokalen UI-Test für Autosave, Tray-Minimierung/Wiederherstellung, Updateprüfung. In Umgebungen ohne zugänglichen Windows-Tray kann der UI-Test mit `-SkipTray` ausgeführt werden; das ersetzt keine Prüfung des Tray-Verhaltens auf dem Desktop. Der Desktop-Smoke-Test ergänzt Monitor-Erkennung, eine 2×2-Pixel-Aufnahme einen kurz sichtbaren Overlay-Rahmen sowie Client-Koordinaten eines eigenen Testfensters. Testdaten liegen unter `artifacts/tests`.

Isolierter App-Test:

```powershell
dotnet run --project Fish.csproj -- --settings-directory E:\Dev\Fish\artifacts\smoke-settings
```

Veröffentlichung für x64:

```powershell
dotnet publish Fish.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/publish
```

Erkennungsqualität, WeakAura-Kompatibilität, Spieleingaben und gemischte DPI-Skalierung müssen zusätzlich in einer echten Spielsitzung geprüft werden.

Die Anwendung nutzt normale Windows-Fenster- und Bildschirm-APIs. Es gibt keine Prozessspeicherzugriffe, Injection, Hooks oder Treiber. Das ist keine Garantie für Nicht-Erkennung durch Anti-Cheat. Die Aufnahme erfasst die sichtbaren Bildschirmpixel innerhalb der Spielfläche; überdeckende Fenster können weiterhin die Erkennung beeinflussen.

Die Übersicht enthält drei einklappbare Bereiche: Protokoll, Live und Analyse. Die Vorschauen zeigen den letzten Bobber-Fund, den letzten Splash-Fund und den aktiven Suchbereich nebeneinander ohne Verzerrung. Das Livebild nutzt vorhandene Scans mit maximal fünf Bildern pro Sekunde; bei pausierter Erkennung wird es ausgeblendet. Fundbilder bleiben bis zum nächsten Sitzungsstart erhalten und werden nicht auf die Festplatte geschrieben.

Publish- und Testausgaben unter artifacts/ und publish/ sind vom Projektinhalt ausgeschlossen, damit alte EXEs nicht erneut in die Veröffentlichung eingebettet werden.

Farbakzente kennzeichnen den Sitzungsstatus (grüne Startlampe / rote Stopplampe), Logstufen und Kategorien sowie die Vorschauen. Beschriftungen ergänzen die Farben.

Analyse zeigt Live-Verläufe für CPU/RAM des Fish-Prozesses in einem gemeinsamen Graphen (CPU links in %, RAM rechts in MB, Achsentexte in Linienfarbe) und Scanzeit mit ms-Y-Achse. CPU und RAM werden auch ohne Sitzung jede Sekunde aktualisiert. Die Gesamtzahlen enthalten Kein Fisch angebissen: ein nach erkanntem Splash beendeter Wurf ohne Timeout und ohne Loot-Bestätigung. Abgebrochene Sitzungen zählen nicht als solches Ergebnis.

Beim Start federt die Schaltfläche kurz auf ihre normale Größe zurück; Stop zeigt einen kurzen seitlichen Impuls. Beide Animationen beeinflussen die Ausführung des Bots nicht.
