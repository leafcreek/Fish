# Architektur der WinUI-Migration

Die WinForms-Ausgangsversion bündelte Zustandserkennung, Bildanalyse und Eingaben im Hauptfenster. Die WinUI-Version trennt diese Aufgaben:

- `Core`: WeakAura-Protokoll, Pixel-Erkennung und serialisierte Bot-Engine.
- `Services`: Bildschirmaufnahme und Spieleingaben.
- `Native`: Win32-Anbindung und transparente, durchklickbare Bereichsrahmen.
- `Configuration`: validierte Einstellungen, automatisches Speichern und Laden neben der EXE.
- `Views`, `ViewModels`, `MainWindow`: Bedienung, Statusbindung und Aktivitätsprotokoll.

Die Engine arbeitet auf einer Kopie der Einstellungen. CancellationToken beendet Aktionen und Countdowns; der Besitzer wartet auf das Ende einer Sitzung, bevor eine neue startet. Gelernte Toleranzen werden beim Stop übernommen.

Bildpuffer werden unabhängig von der Oberfläche erzeugt. Die UI erhält Status und Suchbereiche ohne Bildpuffer. Der Timeout gilt für den gesamten Wurf, auch ohne Bobber-Fund. Die Suchbereiche berücksichtigen negative Bildschirmkoordinaten und werden an die aktuelle WoW-Spielfläche begrenzt. GetClientRect und ClientToScreen liefern den Bezugspunkt ohne Fensterrahmen.

Die Zustandsfarben bleiben für Angeln, Loot, volles Inventar und Tod erhalten. Rot und Weiß bedeuten bereit zum Auswerfen; Schwarz und unbekannte Farben führen zu keiner Spielaktion.

Das Hauptfenster nutzt WinUI 3. Native Bereichsrahmen ersetzen die WinForms-Overlays. Bildschirmaufnahme und Darstellung benötigen keine System.Drawing- oder WinForms-Abhängigkeit.

Defaults, Ausführung und Testbefehle stehen in der [README](../README.md).
