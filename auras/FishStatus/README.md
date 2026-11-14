# Fish-Statuspixel für ForeverAuras

Der fertige Import steht vollständig und ohne Zeilenumbruch in [Import.txt](Import.txt). Erstellt für **ForeverAuras 1.4.0**, Interface **16001**. Er ersetzt den Statusgeber FishHelper; die Fish-Desktop-Anwendung bleibt erforderlich.

1. Fish stoppen und das eigenständige Addon **FishHelper** sowie bisherige Statuspixel-Auren deaktivieren.
2. Den gesamten Inhalt von `Import.txt` kopieren, einschließlich `!WA:2!`.
3. Im Spiel `/fa` öffnen, **Importieren** wählen, den String einfügen und den Import einschließlich des angezeigten benutzerdefinierten Codes bestätigen.
4. Die Aura **Fish - Statuspixel** aktiv lassen. Sie erzeugt oben links im Spielinhalt eine deckende Fläche von 4 × 4 physischen Pixeln; Position und Skalierung werden durch den Code gesetzt.
5. Einmal manuell angeln und die Farben prüfen, danach Fish starten. Angeln liegt weiterhin auf Taste **1**.

Das Präfix `!WA:2!` ist korrekt: ForeverAuras 1.4.0 verwendet es selbst beim Export und akzeptiert zusätzlich `!FA:2!`.

| Farbe | RGB | Bedeutung |
| --- | --- | --- |
| Rot | `#FF0000` | Bereit |
| Blau | `#0000FF` | Eigener Angelkanal |
| Grün | `#00FF00` | Loot im Angelkontext geöffnet |
| Türkis | `#00FFFF` | Bestätigter Inventar-/Taschenfehler |
| Orange | `#FFA500` | Tot oder Geist |
| Schwarz | `#000000` | Unbekannt, anderer Zauber, Weltwechsel, Kampf/API-Sperre oder Fehler |

Die Zustandslogik stammt aus FishHelper: eine Sekunde Übergangszeit nach Kanalende, mindestens 1,5 Sekunden Grün und längere Anzeige bei offenem Lootfenster. Gewöhnliches Loot außerhalb des Angelkontexts zählt nicht. Keine freien Taschenplätze allein bedeuten noch keinen Inventarfehler, weil Stapel Platz bieten können. Weitere Grenzen der zeitlichen Loot-Zuordnung und des Pixelprotokolls stehen in der [FishHelper-Anleitung](../../addon/FishHelper/README.md).

Diese Aura enthält keine Slash-Befehle, Farbtests oder Spieleingaben. Beim Entladen werden Events und Timer abgeschaltet und der zusätzliche Frame ausgeblendet. Nach einem Lua-Fehler bleibt der Pixel schwarz; nach Beheben der Ursache die Aura entladen und wieder laden oder `/reload` ausführen. Bei dauerhaftem Türkis nach Freigeben von Taschenplatz ebenfalls neu laden, falls kein passendes Taschenereignis ausgelöst wurde. Bei ausgeblendeter Spieloberfläche ist auch der Pixel unsichtbar.

**Noch nicht im WoW-Client getestet.** Importkodierung und Rückdekodierung sowie 33 simulierte Zustands-/Lebenszyklustests sind lokal erfolgreich. Die echte Importoberfläche, Secret-/Taint-Regeln, Kanal-IDs und Bildschirmfarben müssen im verwendeten Client geprüft werden. Ein Import kann dessen API-Beschränkungen nicht aufheben.

## Erneut erzeugen und prüfen

`OnShow.lua` enthält den lesbaren Laufzeitcode. `Build.lua` beschreibt die Aura und prüft die Rückdekodierung. `Build.mjs` erzeugt den String erst nach erfolgreichem Testlauf:

```powershell
node auras/FishStatus/Build.mjs
```

Der Builder benötigt Node, `fengari` unter `artifacts/lua-test-runtime/node_modules` (Installation siehe FishHelper-Anleitung) und diese Bibliotheken unter `artifacts/aura-tools`:

- [LibStub.lua](https://github.com/lua-wow/LibStub/blob/master/LibStub.lua)
- [LibSerialize.lua](https://github.com/rossnichols/LibSerialize/blob/master/LibSerialize.lua)
- [LibDeflate.lua](https://github.com/SafeteeWoW/LibDeflate/blob/master/LibDeflate.lua)

Diese Werkzeuge und Bibliotheken werden ausschließlich lokal zum Erzeugen verwendet; zum Importieren wird nur `Import.txt` gebraucht. Node liefert standardkonformes Raw-DEFLATE; LibDeflate dekodiert es zur Prüfung. Der Builder lädt Ganzzahlliterale der Bibliotheken als Gleitkommazahlen, damit ihre Arithmetik wie in WoWs Lua mit Doubles arbeitet und nicht in Fengaris 32-Bit-Ganzzahlen überläuft. Der Aura-Code wird unverändert eingebettet.

Format und Sandbox wurden an [ForeverAuras](https://github.com/neroxrw/foreverauras/tree/600338d31816da62290df68d53cfda7dbc1fdeab), Commit `600338d31816da62290df68d53cfda7dbc1fdeab`, geprüft: insbesondere `Transmission.lua`, `AuraEnvironment.lua` und `RegionTypes/RegionPrototype.lua`. `pcall`, `xpcall` und `SlashCmdList` werden dort für importierten Code gesperrt und deshalb von der Aura nicht verwendet.
