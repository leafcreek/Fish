# Umsetzungsstand

Die WinUI-Version enthält:

- Abbrechbare Bot-Engine mit WeakAura-Erkennung, Auto-Learn, Cast, Loot und Timeout.
- Automatische WoW-Fenstererkennung, Bildschirmaufnahme für die Erkennung und Bereichsrahmen.
- Einstellungseditor mit den in der README beschriebenen Defaults.
- Automatisches Speichern nach Änderungen mit Backup in `appsettings.json` neben der EXE und Laden beim Start.
- Dauerhafter Vordergrundstatus und Unterstützung für inaktives, sichtbares WoW.
- Abbrechbare Countdowns für WoW-Schließen und Windows-Shutdown.
- Anwendungseinstellungen für System-Tray, Tastaturkürzel, Benachrichtigungen, GitHub-Updateprüfung und lokale Diagnoseberichte.

Bei ausgeschaltetem Bot werden keine Spiel- oder Systemaktionen ausgeführt. Einstellungen sind während des Betriebs gesperrt. Bei Fensterbewegung oder Größenänderung werden Bereiche neu berechnet und alte Fundpunkte verworfen. Minimierte Fenster pausieren die Aufnahme und Eingaben.

Tests decken Erkennung, neue Defaults, Speicherung, erneutes Laden und simulierte Engine-Abläufe ab. Der UI-Smoke-Test prüft Startzustand, nativen Vordergrundstatus nach Fokuswechsel und Wiederherstellung, automatisches Speichern beim Schließen und Wiederherstellen beim Neustart mit isolierten Testdaten.

Manuell zu prüfen bleiben eine echte WoW-Sitzung mit aktueller WeakAura sowie Fokuswechsel und mehrere Monitore mit unterschiedlicher DPI-Skalierung. Reale Systemaktionen werden in automatisierten Tests nicht ausgeführt.
