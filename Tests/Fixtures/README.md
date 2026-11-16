# Bild-Regressionen

`bobber-reference.png` ist die ursprüngliche transparente Referenz mit Federn. Sie wird ausschließlich für Tests und die Offline-Vermessung des JSON-Profils verwendet und nicht in die Anwendung eingebettet.

Die ersten Ausschnitte stammen aus dem vom Benutzer bereitgestellten Screenshot `WoWScrnShot_111426_192542.jpg`. Sie enthalten dieselben Bildpixel; BMP-Zwischenstände wurden für das Repository verlustfrei als PNG gespeichert. Die Sonnenuntergang-Aufnahme wurde später als zusätzlicher realer Positivfall geliefert.

- `forever-water.png`: 96 × 96 Pixel Wasser bei (2674, 834), das früher einen Graustufen-Fehlertreffer lieferte.
- `forever-feathered-bobber.png`: 125 × 125 Pixel mit Bobber und beiden Federn. Der bekannte Körperbereich ist im Test ausdrücklich begrenzt.
- `forever-bobber-area.png`: 800 × 400 Pixel mit Bobber und Wasser. Die gefundene Körperposition wird geprüft.
- `forever-water-cursor.png`: 800 × 400 Pixel Wasser mit einem goldenen Mauszeiger. Farbe allein im lokalen Rechteck reichte hier zunächst für einen Fehlertreffer; die vollständigen zusammenhängenden Federgruppen müssen ihn verwerfen.
- `forever-shore.png`: 800 × 400 Pixel Ufer und Wasser mit Text und goldener Richtungsanzeige, ohne Bobber.
- `forever-body.png`: bisherige transparente Körpervorlage ohne Federn (41 × 69). Ein einzelnes Merkmal ohne passendes zweites Merkmal darf keinen Bobber melden.
- `forever-sunset.png`: unveränderte Datei `Screenshot 2026-11-15 193027.png`. Die rote Feder verschmilzt mit dem warmen Wasser; Körper und Blau bleiben erkennbar, die Metallöse kann den Treffer zusätzlich bestätigen.
- `forever-sunset-area.png`: der Suchbereich aus dieser Aufnahme, ohne Rahmenlinie und außerhalb liegende UI, bei (165, 107), 736 × 388 Pixel. Der gleiche Kandidat muss auch innerhalb dieses tatsächlichen Suchbereichs gefunden werden.
- `forever-night.png`: unveränderte Datei `Screenshot 2026-11-15 235008.png`, 883 × 482 Pixel. Der Körper wirkt oliv, die blaue Feder verschmilzt mit dem Wasser. Rot + Metall müssen bei 10 und 80 % erkannt werden, mit Klickpunkt innerhalb des Körpers und zeitlicher Bestätigung.
- `forever-forest.png`: unveränderte Datei `Screenshot 2026-11-16 010428.png`, 1160 × 681 Pixel. Im Suchbereich (176, 232), 784 × 241 Pixel, wirkt der Körper grün und die Öse türkis. Ohne zusätzliche Farbtoleranz gibt es keinen Treffer. RGB-Toleranz 60 muss bei unveränderter Mindestbewertung von 80 % einen bestätigungspflichtigen Treffer im Körper finden.

Die Negativbilder werden auch bei 10 % Mindestwert geprüft. Synthetische Szenen ergänzen fehlende Farben, falsche Anordnung, Rauschen und veränderte Beleuchtung; sie ersetzen keine Aufnahmen aus weiteren Gebieten.
