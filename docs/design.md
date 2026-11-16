# Fish · Kompakt und ruhig

Fish bleibt ein kleines Hilfsfenster: feste Breite von 480 Pixeln, kompakte Höhe von 400 Pixeln, zwei Toleranzregler und eine Start-/Stop-Aktion. Manuelles Vergrößern und Maximieren sind gesperrt. Die Überwachung startet geschlossen und erhöht beim Öffnen ausschließlich die Höhe auf bis zu 820 Pixel. Alle Größen gelten bei 96 DPI. Die Erkennung und ihre Grenzwerte bleiben unverändert.

## Gestaltung

- Mica als Fensterhintergrund, helle Inhaltsflächen und ein gemeinsamer Radius von 12 px. Die native Titelleiste mit Fischsymbol und Windows-Fenstersteuerung bleibt erhalten.
- Petrol für Schwimmer und Suche, Gold für Spritzer und Warten, Grün für Start und Erfolg, Rot für Stop und Zeitablauf. Die Suchrahmen greifen die gleichen Farbfamilien auf.
- Die Statusfläche hat einen zurückhaltenden Mintverlauf und ein kleines Wellenmotiv. Zustand, Aktion und Zeit sind direkt ablesbar. Keine zusätzlichen Kennzahlen oder Erklärtexte.
- Native WinUI-Regler behalten Maus-, Tastatur- und Fokusverhalten bei kompakter Höhe von 26 px. Gering ↔ Hoch steht einmal neben Erkennungstoleranz; die Regler heißen Schwimmer und Spritzer.
- Neue Statusmeldungen blenden in 167 ms ein; ein erfolgreicher Fund erhält einen 250-ms-Impuls. Die bestehende Ergebnisanzeige bleibt 600 ms sichtbar. Die Windows-Einstellung für reduzierte Bewegung wird berücksichtigt. Keine neuen laufenden Dekorationsanimationen oder Animationstimer.
- Überwachung: Schwimmer und Spritzer nebeneinander, Live darunter über die gesamte Breite. Bilder nutzen den verfügbaren Platz; leere Bilder zeigen nur ein dezentes Symbol.
- Suchrahmen sind leicht transparent mit geraden, durchgehend 2 Pixel starken Kanten. So passen Innen- und Außenkontur exakt zusammen. Nur die Griffpunkte sind abgerundet und tragen ✥ und ◢. Der Innenbereich bleibt frei von Rahmenpixeln; Spritzer bleiben unveränderlich und durchklickbar.

## Grundlagen

Recherchiert am 16. November 2026 anhand der Microsoft-Dokumentation:

- [Windows 11 design principles](https://learn.microsoft.com/en-us/windows/apps/design/design-principles): ruhige Hierarchie, klare Formen, gezielte Rückmeldung.
- [Mica](https://learn.microsoft.com/en-us/windows/apps/design/style/mica) und [System backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops): Mica einmal als Grundfläche einsetzen, Inhalte darüber anordnen; automatische Rückfallfarben des Systems beibehalten.
- [Content layout and spacing](https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics): konsistente 4/8/12-px-Abstände und kompakte Schrifthierarchie.
- [Typography](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography): native WinUI-Schriftwahl und gut lesbare Größen.
- [Fluent 2 Motion](https://fluent2.microsoft.design/motion), [Motion in Windows](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion) und [Implicit transitions](https://learn.microsoft.com/en-us/windows/apps/develop/motion/implicit-transitions): kurze, zweckgebundene Übergänge und bestehende Steuerelemente verwenden.
