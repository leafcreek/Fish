# Splash-Erkennung durch Bildvergleich

Während des Angelns wird der Splash-Bereich im eingestellten Scan-Intervall aufgenommen und mit seinem unmittelbar vorherigen Rohbild verglichen. Pixel gelten als verändert, wenn mindestens ein RGB-Kanal die Rauschtoleranz erreicht. Alpha wird ignoriert. Der Prozentwert ist die Anzahl dieser Pixel geteilt durch die gesamte Bildfläche. Die rote Vorschau-Markierung wird in eine separate Bildkopie gezeichnet und fließt nicht in den nächsten Vergleich ein.

Das erste Bild dient als Referenz. Die Erkennung wird frühestens nach der Kalibrierzeit und drei Bildvergleichen scharf. Ein einzelner ausreichend großer Ausschlag kann danach auslösen; ein zweiter Ausschlag wird nicht verlangt, damit auch kurze Effekte erfasst werden können.

## Startwerte

| Einstellung | Standard | Wirkung |
| --- | --- | --- |
| Scan-Intervall | 150 ms | Wartezeit zwischen den Scans; Aufnahme und Verarbeitung kommen hinzu |
| Pixel-Rauschtoleranz | 20 | Mindeständerung eines RGB-Kanals; größere Werte ignorieren mehr kleine Farbänderungen |
| Mindeständerung | 5 % | Untergrenze des nötigen Anteils veränderter Pixel; einstellbar 1–80 % |
| Kalibrierung | 500 ms | Anfangsphase nach neuer Referenz; einstellbar 300–3000 ms |
| Wasserbewegung berücksichtigen | Ein | Erhöht die wirksame Grenze gegenüber gewöhnlicher Bewegung |

Die Baseline der normalen Bewegung ist ein gleitender Mittelwert mit Gewicht 0,15. Bei eingeschalteter Anpassung gilt `Grenze = min(80, max(Mindeständerung, 2,5 × Normalbewegung + 2))`, jeweils in Prozentpunkten. Nach der Kalibrierung werden Ausschläge oberhalb der Grenze und erkannte Treffer nicht in die Baseline eingelernt. Die Baseline gilt nur für die aktuelle Beobachtung und wird nicht in Einstellungen gespeichert. Ohne Anpassung gilt die konfigurierte Mindeständerung direkt.

## Schutz vor Fehlertreffern

Neben dem Gesamtanteil muss mindestens eine zusammenhängende Gruppe veränderter Pixel ein Prozent der Bildfläche umfassen, mindestens vier Pixel. Diagonale Nachbarn zählen zur selben Gruppe. Viele isolierte Schneepixel reichen daher nicht aus.

Bei einer Änderung von mindestens 85 % der Bildfläche wird ein Bildwechsel angenommen. Der Bot verwirft den Fundpunkt, sucht den Bobber neu und kalibriert erneut. Fensterbewegung, Größenänderung und unterbrochener Status verwerfen ebenfalls die alte Referenz. Kleinere Kamerabewegungen können trotzdem einem Splash ähneln; für die Beurteilung ist eine ruhige Kamera sinnvoll.

Nach einem Treffer wird pro Wurf nur eine Loot-Anfrage gesendet, auch wenn der Effekt verschwindet, wieder erscheint oder das Fenster anschließend bewegt wird. Geklickt wird auf den gespeicherten Körper-Anker des Bobbers. Die Position veränderter Wasserpixel bestimmt nicht den Klickpunkt. Erst der Statuspixel bestätigt tatsächlichen Loot. Ein neuer Wurf setzt die Sperre zurück.

## Live-Prüfung

Die Live-Vorschau zeigt rote Änderungen sowie aktuellen Änderungsanteil, wirksame Grenze, normale Bewegung und Kalibrier-/Auslösestatus. Das letzte Splash-Fundbild behält die Messwerte seines Treffers. Das Protokoll hält bei einer Loot-Anfrage die Änderung, Grenze, größte zusammenhängende Gruppe und den Klickpunkt fest.

Zuerst gewöhnliche Wasserbewegung beobachten, dann die Ausschläge bei echten Bissen vergleichen. Bei vielen kleinen Änderungen kann die Pixel-Rauschtoleranz erhöht werden. Eine niedrigere Mindeständerung reagiert empfindlicher; bei eingeschalteter Anpassung kann die wirksame Grenze weiterhin höher liegen. Einzelbilder und Simulationen belegen keine zuverlässige Erkennung in einer echten Angelsitzung.

Die bisherigen Einstellungen `SplashAreaTolerance`, `SplashAreaAutoLearn` und `PerformanceSkip` werden ignoriert. Es wird immer jeder Pixel des Splash-Bereichs verglichen; dessen gespeicherte Größe und Versätze bleiben erhalten.
