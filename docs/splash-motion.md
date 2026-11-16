# Splash-Erkennung durch Bildvergleich

Während des Angelns wird der Splash-Bereich im eingestellten Scan-Intervall aufgenommen und mit seinem unmittelbar vorherigen Rohbild verglichen. Pixel gelten als verändert, wenn mindestens ein RGB-Kanal die Rauschtoleranz erreicht. Alpha wird ignoriert. Der Prozentwert ist die Anzahl dieser Pixel geteilt durch die gesamte Bildfläche. Die rote Vorschau-Markierung wird in eine separate Bildkopie gezeichnet und fließt nicht in den nächsten Vergleich ein.

Das erste Bild dient als Referenz. Die Erkennung wird frühestens nach der Kalibrierzeit und drei Bildvergleichen scharf. Ein einzelner ausreichend großer Ausschlag kann danach auslösen; ein zweiter Ausschlag wird nicht verlangt, damit auch kurze Effekte erfasst werden können.

## Startwerte

| Einstellung | Standard | Wirkung |
| --- | --- | --- |
| Scan-Intervall | Fest 100 ms | Wartezeit zwischen den Scans; Aufnahme und Verarbeitung kommen hinzu |
| Pixel-Rauschtoleranz | 50 | Mindeständerung eines RGB-Kanals; größere Werte ignorieren mehr kleine Farbänderungen |
| Mindeständerung | 10 % | Untergrenze des nötigen Anteils veränderter Pixel |
| Kalibrierung | 1000 ms | Feste Anfangsphase nach neuer Referenz |
| Wasserbewegung berücksichtigen | Immer ein | Erhöht die wirksame Grenze gegenüber gewöhnlicher Bewegung |

Die Baseline der normalen Bewegung ist ein gleitender Mittelwert mit Gewicht 0,15. Die immer aktive Anpassung verwendet `Grenze = min(80, max(Mindeständerung, 2,5 × Normalbewegung + 2))`, jeweils in Prozentpunkten. Nach der Kalibrierung werden Ausschläge oberhalb der Grenze und erkannte Treffer nicht in die Baseline eingelernt. Die Baseline gilt nur für die aktuelle Beobachtung und wird nicht in Einstellungen gespeichert.

## Schutz vor Fehlertreffern

Neben dem Gesamtanteil muss mindestens eine zusammenhängende Gruppe veränderter Pixel ein Prozent der Bildfläche umfassen, mindestens vier Pixel. Diagonale Nachbarn zählen zur selben Gruppe. Viele isolierte Schneepixel reichen daher nicht aus.

Bei einer Änderung von mindestens 85 % der Bildfläche wird ein Bildwechsel angenommen. Der Bot verwirft den Fundpunkt, sucht den Bobber neu und kalibriert erneut. Fensterbewegung, Größenänderung und unterbrochene Verfügbarkeit verwerfen ebenfalls die alte Referenz. Kleinere Kamerabewegungen können trotzdem einem Splash ähneln; für die Beurteilung ist eine ruhige Kamera sinnvoll.

Nach einem Treffer wird auf den gespeicherten Körper-Anker des Bobbers geklickt. Die Position veränderter Wasserpixel bestimmt nicht den Klickpunkt. Der [Angelablauf](fishing-cycle.md) wechselt nach gesendeter Eingabe in eine feste Wartezeit, stoppt die Bildvergleiche dieses Wurfs und wirft anschließend erneut aus. Wird die Eingabe wegen eines Fenster- oder Fokuswechsels nicht gesendet, beginnt eine neue Bobber-Suche innerhalb der verbleibenden Angelzeit. Tatsächlicher Loot wird nicht bestätigt.

## Live-Prüfung

Die Überwachung zeigt die Fundbilder für Schwimmer und Spritzer oben nebeneinander und die große Liveansicht darunter. Rote Änderungen bleiben in den Bildern markiert; zusätzliche Messwerte und Statuszeilen unter den Bildern werden nicht angezeigt.

Ist die Live-Vorschau eingeklappt oder das Fenster verborgen, läuft die Erkennung weiter, ohne für jeden Scan ein Vorschaubild aufzubereiten. Ein bestätigter Biss erzeugt weiterhin sein markiertes Fundbild. Die Vorschau verändert weder Vergleichsreferenz noch Auslöseschwelle.

Der Spritzer-Toleranz-Slider koppelt Rauschtoleranz und Mindeständerung und lässt sich bei aktivem Bot ändern. Bei 0 gelten die Werte 100 / 25 %, bei 50 die Werte 50 / 10 % und bei 100 die Werte 10 / 1 %. Zwischenwerte folgen einer festen weichen Kurve. Frühere individuell gespeicherte Einzelwerte werden auf die nächstpassende Reglerstellung übertragen und nicht mehr separat gespeichert. Änderungen verwerfen die bisherige Bewegungsreferenz und kalibrieren neu. Der Bereich bleibt fest 50 × 50 px ohne Versatz, das Scan-Intervall beträgt 100 ms zusätzlich zur Verarbeitung. Kalibrierung ist fest 1000 ms und Wasseranpassung immer aktiv.

Zuerst gewöhnliche Wasserbewegung beobachten, dann die Ausschläge bei echten Bissen vergleichen. Bei Fehlertreffern den Slider nach links, bei übersehenen Bissen vorsichtig nach rechts bewegen. Die Wasseranpassung kann die wirksame Prozentgrenze weiterhin erhöhen. Einzelbilder und Simulationen belegen keine zuverlässige Erkennung in einer echten Angelsitzung.

Es wird jeder Pixel des Splash-Bereichs verglichen. Referenz-, Änderungs- und Suchpuffer werden wiederverwendet. Unveränderte Bilder benötigen weder eine Suche nach Pixelgruppen noch eine zusätzliche markierte Kopie. Ohne angeschlossene Vorschau wird ebenfalls keine markierte Bildkopie erzeugt. Laufende Vergleiche und die Gruppensuche reagieren auf Stop.
