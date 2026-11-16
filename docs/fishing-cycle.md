# Angelablauf

1. **Warten auf WoW:** Erst bei sichtbarem, verfügbarem Spielfenster starten. Ein neues Fenster verwirft den vorherigen Wurf.
2. **Auswerfen:** Den eingestellten Angeln-Hotkey senden. Nur bei gesendeter Eingabe beginnt ein Wurf. Der Standard ist `1`; die Belegung lässt sich während des Betriebs ändern.
3. **Anwurf:** Zwei Sekunden warten, damit der Bobber aufsetzt. Diese Phase zählt nicht zum Bobber-Timeout. Die Fortschrittsleiste zeigt dafür separat 0 bis 2 Sekunden.
4. **Bobber suchen:** Mindestens zwei der vier Merkmale Blau, Rot, Körper und Metallöse müssen in Größe und Anordnung zusammenpassen. Jede Zweierkombination ist erlaubt; genau zwei Merkmale müssen in drei aufeinanderfolgenden Aufnahmen bestätigt werden, ab drei Merkmalen wird der Treffer sofort übernommen. Ohne Körperfarbe wird der Körper-Klickpunkt aus den übrigen Merkmalen berechnet. Die Referenzen stammen aus der PNG; ein einzelnes Merkmal reicht nicht aus. Ab Beginn der Suche stehen volle fünf Sekunden zur Verfügung; ohne bestätigten Treffer wird anschließend erneut ausgeworfen, insgesamt frühestens etwa sieben Sekunden nach dem Wurf. Die Bestätigung verlängert den Timeout nicht. Spät berechnete Ergebnisse eines bereits abgelaufenen Wurfs werden verworfen.
5. **Splash beobachten:** Nach dem Fund eine frische Bewegungsreferenz kalibrieren. Die gesamte Angelzeit bleibt standardmäßig auf 25 Sekunden ab Wurf begrenzt. Ohne Splash erneut auswerfen.
6. **Looten:** Bei einem Biss auf den gespeicherten Körper-Anker klicken. Nur eine gesendete Loot-Eingabe schließt den Wurf ab. Wird die Eingabe wegen fehlender Verfügbarkeit oder Fokus nicht gesendet, den Bobber neu suchen; die maximale Angelzeit läuft weiter.
7. **Warten und wiederholen:** Standardmäßig 1000 ms nach der gesendeten Loot-Eingabe warten. In dieser Phase keine weiteren Aufnahmen oder Eingaben dieses Wurfs ausführen. Danach erneut auswerfen.

Die Zeitgrenzen werden vor Aktionen und nach aufwendiger Erkennung geprüft. Verarbeitung und Scan-Intervall können die Reaktion verzögern. Ein Timeout führt zum neuen Wurf; die Konfiguration muss nicht neu geladen werden.

## Änderungen und Unterbrechungen

Die Engine übernimmt atomar eine unveränderliche Konfiguration. Ein neuer Hotkey gilt ab dem nächsten Wurf. Änderungen an Bobber-Bereich, Mindestbewertung oder Farbtoleranz verwerfen den Fundpunkt. Änderungen an Splash-Rauschtoleranz oder Mindeständerung verwerfen die Referenz. Zeiten, Kalibrierung, Wasseranpassung und Splash-Geometrie sind fest vorgegeben. Noch laufende Ergebnisse der alten Konfiguration dürfen keine Eingabe auslösen.

Fensterbewegung, Größenänderung und großflächiger Bildwechsel verwerfen alte Fundpunkte. Erneute Bobber-Suchen bekommen einen eigenen Suchzeitraum, verlängern aber nicht die gesamte Wurfdauer. Minimiertes WoW pausiert Aufnahme und Eingaben. Eine abgelaufene Wurfdauer wird nach Wiederherstellung behandelt. Vorübergehend außerhalb liegende Suchbereiche pausieren, bis sie korrigiert werden. Die Rahmen folgen nur dem aktuellen Spielfenster; ein Splash-Rahmen erscheint erst nach Bobber-Fund.

Der Button in der Statuskarte wechselt nach **Starten** zu **Stoppen**. **Stoppen** beendet die Sitzung und verhindert weitere Eingaben. Während des Abbruchs bleibt der Button kurz deaktiviert. Bereits gedrückte Tasten und Maustasten werden freigegeben. Danach bietet derselbe Button wieder **Starten** an. Ein neuer Start hat neue Erkennungsreferenzen und beginnt mit dem Auswerfen.

Die Mausgriffe des Bobber-Rahmens pausieren Aufnahme und Eingaben während des Ziehens. Nach dem Loslassen wird die neue Geometrie übernommen und der Bobber mit frischer Referenz gesucht. Die maximale Wurfzeit läuft weiter; ist sie inzwischen abgelaufen, wird nach Abschluss der Bearbeitung erneut ausgeworfen. Escape und Fensteränderungen verwerfen die Geste.

Der sichtbare Splash-Rahmen ist durchklickbar, besitzt keine Mausgriffe und bleibt fest 50 × 50 px mittig am erkannten Bobber. Nur der Bobber-Suchbereich lässt sich über seine Griffe verschieben und skalieren. Änderungen bauen die Oberfläche nicht neu auf und werden automatisch gespeichert.

Die visuelle Erkennung bestätigt weder Zauberausführung noch tatsächlichen Loot-Erfolg. Fehlgeschlagene Würfe, Tod und Inventarstatus können damit nicht zuverlässig erkannt werden. Tests mit simulierten Bildern ersetzen die Prüfung im Spiel nicht.
