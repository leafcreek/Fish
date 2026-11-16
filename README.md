# Fish

Kompakter WinUI-3-Angelbot für World of Warcraft auf .NET 10. Die Erkennung arbeitet mit sichtbaren Bildschirmaufnahmen, ohne Addon oder Statuspixel.

## Benutzung

Die aktuelle Einzeldatei liegt unter **`publish/Fish.exe`**. WoW muss sichtbar sein. Den **Angeln-Hotkey** passend zur Spielbelegung eingeben oder mit **Setzen** aufnehmen. Standard ist `1`; Kombinationen wie `Ctrl+F8` sind möglich. Escape bricht die Aufnahme ab.

Der Button in der Statuskarte wechselt zwischen **Starten** und **Stoppen**. Ein neuer Start beginnt mit einem frischen Wurf. Nach dem Auswerfen wartet Fish zwei Sekunden und sucht anschließend bis zu fünf Sekunden nach dem Schwimmer. Nach dessen Fund erkennt der Vergleich aufeinanderfolgender Bilder einen Spritzer und löst den Loot-Klick aus. Die maximale Wurfdauer beträgt 25 Sekunden ab Wurf; nach einem Klick wartet Fish eine Sekunde. Ausführlich: [Angelablauf](docs/fishing-cycle.md).

Die Fortschrittsleiste zeigt Anwurf, Schwimmer-Suche, Spritzer-Beobachtung und Loot-Wartezeit getrennt. Treffer leuchten kurz grün, Timeouts rot.

Zum Auswerfen und Looten aktiviert Fish kurz das Spielfenster und stellt den vorherigen Fokus anschließend wieder her. Ein gedrückter Modifier oder eine gehaltene Angeltaste verschiebt den nächsten Wurf. Fenster und Suchbereich werden vor Eingaben sowie nach Scans erneut geprüft.

## Erkennungstoleranz

Die zwei Slider füllen die verfügbare Breite. Links steht **Gering**, mittig **Schwimmer** beziehungsweise **Spritzer**, rechts **Hoch**. Die interne Skala reicht von 0 bis 100; Zahlenfelder und Tooltips gibt es nicht.

| Slider | Gering: 0 | Mitte: 50 | Hoch: 100 |
| --- | --- | --- | --- |
| Schwimmer: Mindestbewertung / Farbtoleranz | 95 % / 0 | 50 % / 50 | 1 % / 90 |
| Spritzer: Pixel-Rauschtoleranz / Mindeständerung | 100 / 25 % | 50 / 10 % | 10 / 1 % |

Diese Eckpunkte gelten für neue Einstellungen. Ältere Einzelwerte bleiben als Referenz bei Reglerstellung 50 erhalten. Die weichen Kurven werden immer aus dieser gespeicherten Referenz berechnet; wiederholtes Verstellen oder Neustarten verstärkt die Werte nicht erneut. Höhere Toleranz kann mehr Fehlertreffer bedeuten.

Die [Schwimmer-Erkennung](Assets/Detection/README.md) verlangt mindestens zwei räumlich passende Merkmale aus blauer Feder, roter Feder, Körper und Metallöse. Genau zwei Merkmale benötigen drei frische Aufnahmen zur Bestätigung, ab drei Merkmalen wird ein Treffer sofort akzeptiert. Die [Spritzer-Erkennung](docs/splash-motion.md) berücksichtigt normale Wasserbewegung, zusammenhängende Änderungen und großflächige Bildwechsel. Live-Änderungen gelten während der Sitzung; eine veränderte Schwimmer-Abstimmung verwirft alte Fundpunkte, eine veränderte Spritzer-Abstimmung kalibriert neu.

Das Schwimmer-Profil ist vollständig in `Assets/Detection/bobber.json` gespeichert. **Die Anwendung benötigt kein Schwimmer-PNG und berechnet kein Template beim Start.** Die ursprüngliche PNG liegt ausschließlich als Regressionstest-Vorlage unter `Tests/Fixtures/bobber-reference.png`.

## Bereiche und Überwachung

Der Schwimmer-Bereich startet bei 600 × 300 Pixeln ohne Versatz. Seine Mausgriffe verschieben und skalieren ihn; Escape verwirft die Geste. Während der Bearbeitung ruhen Aufnahme und Eingaben. Nach dem Loslassen wird mit frischer Referenz gesucht. Die Gesamtwurfdauer läuft weiter.

Der Spritzer-Bereich bleibt fest 50 × 50 Pixel mittig am erkannten Schwimmer und besitzt keine Mausgriffe. Er ist erst nach einem bestätigten Schwimmer-Fund sichtbar. Minimiertes Fish blendet beide Rahmen und die Griffe aus. Minimiertes WoW unterbricht Aufnahme und Eingaben.

Die aufklappbare **Überwachung** zeigt Schwimmer und Spritzer nebeneinander, darunter Live über die volle Breite. Die Bilder füllen ihre Flächen ohne festes Seitenverhältnis und passen sich Fensterbreite und -höhe an. Es gibt nur Überschriften. Fundbilder bleiben bis zum nächsten Start erhalten; Livebilder werden auf höchstens fünf pro Sekunde begrenzt und bei verborgener Überwachung nicht aufbereitet. Kleine Fenster bleiben per Mausrad bedienbar; Scrollleisten sind ausgeblendet.

Fish verwendet eine feste helle Darstellung, bleibt im Vordergrund und hat kein Tray-Symbol, Protokoll, Statistikmodul oder Update-System. Schließen beendet die Anwendung. Fehlermeldungen erscheinen direkt im Fenster.

## Einstellungen und Projektaufbau

`appsettings.json` liegt neben der EXE. Änderungen werden nach 400 ms Eingabepause und beim Schließen atomar gespeichert, mit `.bak`-Sicherung. Nicht lesbare oder neuere Konfigurationen werden nicht überschrieben. Zeiten, Spritzer-Geometrie und Wasseranpassung stammen ausschließlich aus `FishingDefaults`.

- `Core/`: Erkennung, Angelablauf, Fortschritt und Vorschaupuffer.
- `Configuration/`: gespeicherte Werte, Toleranzkurven und unveränderliche Laufzeitoptionen.
- `Native/` und `Services/`: Fenster, Aufnahme, Eingaben und Rahmen.
- `Views/MainWindow/`: Fensterlayout, Lebensdauer, Darstellung und Speicherung.
- `Views/`: Hotkey-Editor, Regler und Überwachungsansicht.
- `Assets/`: Goldfisch-Icon und eingefrorenes Erkennungsprofil.
- `Tests/`: Simulationen, Bildregressionen und Desktop-/UI-Prüfungen.
- `publish/`: eine aktuelle ausführbare Version samt Benutzereinstellungen.
- `artifacts/`: Prüfberichte und isolierte Testausgaben; keine Release-Sammlung.

Die [Architektur](docs/architecture.md) beschreibt die Zuständigkeiten. Die früher angeforderte PDF bleibt als historisches Dokument unter `docs/archive/` erhalten; maßgeblich sind die aktuellen Markdown-Dokumente.

## Bauen und prüfen

```powershell
dotnet build Fish.csproj
dotnet run --project Tests/Fish.Tests.csproj
dotnet publish Fish.csproj -c Release -p:PublishProfile=win-x64
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/CompactUiSmoke.ps1 -Executable publish/Fish.exe
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/Smoke-Ui.ps1 -Executable publish/Fish.exe -NoSession -NativeOverlay
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/SessionUiSmoke.ps1 -Executable publish/Fish.exe
```

`--desktop-smoke` ergänzt native Aufnahme- und Ressourcenprüfungen; `--overlay-only` prüft die Griffe ausschließlich an einem eigenen Testfenster. UI-Tests verwenden isolierte Einstellungen unter `artifacts/ui-tests/`. Der Sitzungstest verwendet einen garantiert außerhalb des Spielfensters liegenden Bereich und sendet keine Spieleingaben. Reale Erkennungsqualität und Loot-Erfolg müssen weiterhin im Zielclient beurteilt werden.
