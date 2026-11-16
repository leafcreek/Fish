# Architektur

## Ablauf und Konfiguration

`BotEngine` besitzt die Lebensdauer einer Sitzung, übernimmt Konfigurationsänderungen atomar und leitet Status- und Bildereignisse weiter. Parallele Läufe derselben Engine sind ausgeschlossen. `FishingSession` führt den Ablauf seriell aus: Auswerfen, zwei Sekunden Anwurf, fünf Sekunden Schwimmer-Suche, Spritzer-Beobachtung und Loot-Wartezeit. Das Gesamtlimit bleibt bei 25 Sekunden ab Wurf.

`BotOptions` ist das gespeicherte UI-Modell. `SettingsStore` validiert und migriert es und schreibt atomar mit Sicherung. Die getrennt gespeicherten Referenzpaare erhalten die bisherige Abstimmung bei Toleranz 50. `DetectionSensitivity` erzeugt daraus über weiche, monotone Kurven die effektiven Werte. `FishingOptions` enthält nur veränderliche Sitzungswerte; feste Zeiten, Geometrie und Wasseranpassung werden aus `FishingDefaults` gelesen und nicht pro Schnappschuss kopiert.

Bereichsbearbeitung ist eine eigenständige Eingabesperre. `SetAreaEditing` erhöht bei jeder Änderung eine Versionsnummer. Damit wird auch eine Aufnahme verworfen, während der eine komplette Bearbeitung zwischen zwei Schleifendurchläufen stattfand. Stop und Konfigurationsänderungen werden vor Eingaben und nach aufwendigen Scans erneut geprüft.

## Schwimmer und Spritzer

Die Laufzeitreferenz stammt ausschließlich aus `Assets/Detection/bobber.json`. Sie enthält die vier Farben, Merkmalsgrößen, Lagebeziehungen und den Körper-Anker. Fehlende oder ungültige Profile führen zu einem Fehler. Es gibt keine PNG-Decodierung, Template-Vorbereitung, automatische Neumessung oder Graustufen-Suche im Produktionsprogramm.

`BobberSegmentation` klassifiziert Pixel und bildet zusammenhängende Komponenten. Gepoolte Arbeitsarrays werden vor der räumlichen Bewertung freigegeben. `BobberDetector` prüft vollständige Komponenten gegen vorbereitete Größen-, Drehungs- und Streckungshypothesen. Alle sechs Zweierkombinationen sind erlaubt. Metall benötigt zusätzlich eine geschlossene Öffnung mit Ansatz. Dünne Reflexe und große zusammenhängende Wasserflächen dürfen keine vollständigen Merkmale ersetzen.

Die Mindestbewertung beurteilt Farbähnlichkeit, Geometrie und Pixelmenge. Zusätzliche RGB-Toleranz erweitert die Pixelzulassung, erhöht aber nicht die Qualität desselben Pixels. `BobberConfirmation` verlangt bei genau zwei Merkmalen drei frische, räumlich passende Beobachtungen; ab drei Merkmalen wird ein Fund sofort übernommen. Ohne Körperfarbe wird dessen Klickpunkt aus den übrigen Merkmalen berechnet.

`SplashMotionDetector` vergleicht aufeinanderfolgende Rohbilder und lernt die normale Wasserbewegung. Er prüft den Gesamtanteil und zusammenhängende Änderungen. Große Bildwechsel verwerfen die Referenz. Vergleichs-, Markierungs- und Suchpuffer werden wiederverwendet; Abbruch wird auch innerhalb größerer Pixeloperationen geprüft. Die numerischen Messergebnisse bleiben beim Detektor und im Ablauf, statt als ungenutzte UI-Metadaten durch die Vorschau zu laufen.

## Bilder und Oberfläche

`PreviewAvailable` übergibt ein Bild, dessen `PreviewKind` und die Information, ob es ein bestätigter Fund ist. `PreviewBuffer` behält unabhängige, begrenzte Kopien für Live, Schwimmer und Spritzer. Fundbilder werden auch ohne sichtbare Liveansicht erzeugt. Die Liveansicht wird auf fünf Bilder pro Sekunde begrenzt. Zeitstempel, alte Messwerttexte und deren Transportfelder sind entfernt.

`DetectionPreviews` zeigt Schwimmer und Spritzer oben nebeneinander und Live darunter. Die Bildflächen nutzen den verfügbaren Platz in Breite und Höhe; `Stretch.Fill` löst das ursprüngliche Seitenverhältnis. `MainWindow.Previews` berechnet die verfügbare Höhe unabhängig vom aktuellen Scrollversatz. Kleine Fenster behalten Mausradnavigation ohne sichtbare Scrollleisten. Unter den Bildern gibt es keine Texte.

Die Fensterdateien liegen gemeinsam unter `Views/MainWindow/`. Lebensdauer und Status, Sitzung, Bereichseinstellungen, Darstellung, Platzierung, Speicherung, Fortschritt und Überwachung sind nach Zuständigkeit getrennt. `Appearance` hält wiederverwendete Farben und Brushes. Die Fortschrittsanzeige erstellt im Leerlauf keine fortlaufenden Texte oder Brushes. Die feste helle Darstellung benötigt keine Theme-Umschaltung. Tooltips werden nicht angelegt; Framework-Tooltips sind unterdrückt.

## Windows-Anbindung

`GameWindow` liest Identität, Sichtbarkeit und Client-Geometrie. `BotDesktop` verbindet diese Prüfungen mit Aufnahme und Eingaben. `ScreenCapture` verwendet einen wiederverwendeten Windows-DIB und DC; zurückgegebene Bilder besitzen eigene verwaltete Pixel. Eingaben gehen nur an das weiterhin passende Fenster. `KeyboardInput` sendet zusammengehörige Tastenkombinationen mit Freigaben, und der vorherige Fokus wird nach einer Aktion wiederhergestellt.

`DetectionOverlays` verwaltet zwei durchklickbare Rahmen und nur am Schwimmer zwei Mausgriffe. `RegionEdit` berechnet Bildschirmgeometrie und gespeicherte Versätze. Mausaufnahme, Escape, Capture-Verlust und Fensterwechsel begrenzen beziehungsweise verwerfen eine Geste. Die Griffe sind aus Erkennungsaufnahmen ausgeschlossen.

`WindowEnvironment` behandelt Bildschirm- und DPI-Änderungen unabhängig von einem Tray-Symbol. `WindowIcon` lädt das Goldfisch-Symbol aus dem Apphost und hält kleine und große Icon-Handles bis zum Schließen gültig. `WindowZOrder` sichert den Vordergrundstatus zusätzlich zum WinUI-Presenter ab. `WindowPlacement` begrenzt die gespeicherte Position auf die aktuelle Arbeitsfläche.

## Prüfungen und Ausgaben

Referenz-PNG, PNG-Decoder und Profilvermessung liegen ausschließlich unter `Tests/`. Sie erzeugen synthetische Beleuchtungs-, Größen- und Perspektivfälle und kontrollieren das eingefrorene JSON-Profil. Reale Positiv- und Negativaufnahmen prüfen Treffer, Fehlertreffer und Klickpunkte. `--benchmark-bobber` und `--compare-bobber` vergleichen Ergebnisse einschließlich Bewertung und Merkmalsgruppen.

Simulierte Sitzungen prüfen Fristen, neue Toleranzen, Fensterwechsel, Bearbeitung, Stop und Eingabefehler. Native Prüfungen ergänzen Aufnahme, Ressourcen und Griffe. UI-Prüfungen verwenden isolierte Konfigurationen und eigene Fenster. Das Publish-Profil erzeugt eine aktuelle Einzeldatei unter `publish/`; Prüfausgaben werden unter `artifacts/` gebündelt.
