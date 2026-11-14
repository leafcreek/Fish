# FishHelper

Schlankes WoW-Forever-Addon für den Statuspixel von Fish. Version **0.1.0**, TOC-Interface **16001**. Ohne WeakAuras, externe Bibliotheken, SavedVariables, dauerhaftes Polling oder zusätzliche Bilddateien. Laufzeitdateien: `FishHelper.toc` und `FishHelper.lua`.

## Installation

1. In Battle.net die **Forever-Installation** auswählen und ihren Installationsordner öffnen.
2. Diesen kompletten Ordner `FishHelper` nach `Interface\AddOns` dieser Installation kopieren. Das Ergebnis muss `Interface\AddOns\FishHelper\FishHelper.toc` sein; keinen zusätzlichen Ordner dazwischen schachteln.
3. Den WoW-Client nach der ersten Installation vollständig neu starten und **FishHelper** in der Addon-Liste aktivieren.
4. Einen bisherigen WeakAura-Statuspixel an derselben Position deaktivieren, damit sich die Anzeigen nicht überdecken.
5. Mit `/fishhelper status` Clientversion und Zustand ansehen. Angeln weiterhin auf **Taste 1** legen, wenn Fish verwendet wird.

Die TOC-Version stammt aus dem recherchierten Forever-Stand 1.60.1 / Interface 16001. Für einen neueren Client dessen Interface und die API-Kompatibilität prüfen; eine geänderte TOC-Nummer allein weist keine Kompatibilität nach.

## Signale

Die deckende Farbfläche liegt oben links im Spielinhalt und wird auf **4 × 4 physische Pixel** ausgelegt. Fish liest davon Pixel `(0,0)`. Farben und Koordinate entsprechen seinem bisherigen WeakAura-Protokoll, daher ist keine Änderung an der Desktop-Anwendung nötig. Diese verwendet teilweise weiterhin die Bezeichnung „WeakAura“.

| Farbe | RGB | Bedeutung |
| --- | --- | --- |
| Rot | `#FF0000` | Bereit; weder ein anderer Zauber noch ein bekannter Sperrzustand aktiv |
| Blau | `#0000FF` | Eigener Angelkanal erkannt |
| Grün | `#00FF00` | Loot innerhalb des Angelkontexts geöffnet; keine Bestätigung einer Fisch-Stückzahl |
| Türkis | `#00FFFF` | Inventar-/Taschenfehler bestätigt |
| Orange | `#FFA500` | Tot oder Geist |
| Schwarz | `#000000` | Initialisierung, Kampf/API-Sperre, anderer Zauber, Übergang, deaktiviert oder Fehler |

Nach Kanalende bleiben bis zu **1 Sekunde** für verzögertes Angel-Loot. In diesem Übergang ist die Anzeige schwarz. Grün bleibt mindestens **1,5 Sekunden** und bei offenem Lootfenster bis zu dessen Schließen stehen. Doppelte Loot-Events verlängern die Haltezeit nicht. Die Zuordnung ist zeitlich: Fremdes Loot unmittelbar nach einem Angelkanal lässt sich damit nicht zweifelsfrei unterscheiden. Außerhalb dieses Kontexts erzeugt gewöhnliches Loot keine grüne Phase.

Null freie Slots allein erzeugen keinen Voll-Status, da vorhandene Stapel noch Platz bieten können. Türkis entsteht durch einen bestätigten Inventarfehler. Es wird zurückgenommen, sobald ein freier Platz im Rucksack oder einer normalen getragenen Tasche erkannt wird. Plätze in Spezial-/Reagenztaschen und Bank zählen dafür nicht. Alternativ lässt sich der Fehler nach manueller Prüfung mit `/fishhelper reset` zurücksetzen.

Tod hat Vorrang vor den übrigen Spielzuständen. Deaktivieren mit `off` erzwingt Schwarz. Fehler bei erforderlichen Events/APIs und unerwartete Lua-Fehler erzeugen Schwarz; Laufzeitfehler bleiben bis `reset` oder `/reload` gesetzt.

Schwarz verhindert unmittelbare Wurf-/Loot-Eingaben in Fish. Seine vorhandenen Inaktivitäts- und Folgeaktions-Countdowns werden dadurch nicht deaktiviert. Zum Beenden einer Desktop-Sitzung weiterhin **Stop** in Fish verwenden.

## Diagnose

| Befehl | Wirkung |
| --- | --- |
| `/fishhelper` oder `/fishhelper status` | Addonversion, Zustand, Grund, Client-Build/Interface und zuletzt gelesene Kanal-ID |
| `/fishhelper test` | 12 Sekunden Farbtest: Schwarz, Rot, Blau, Grün, Türkis, Orange, jeweils 2 Sekunden; danach aktueller Spielzustand |
| `/fishhelper trace` | 60 Sekunden Ereignisdiagnose mit Zustand und lesbarer Kanal-ID; keine Ausgabe geschützter Event-Payloads |
| `/fishhelper off` | Statusgeber bis `on`/`reset` oder Reload deaktivieren; Schwarz |
| `/fishhelper on` | Statusgeber aktivieren und aktuellen Zustand neu abfragen |
| `/fishhelper reset` | Inventar-/Laufzeitfehler löschen, aktivieren und Zustand neu abfragen; fehlende erforderliche Events/APIs bleiben ein Fehler |

**Vor dem Farbtest Fish stoppen**, da die Testfarben echte Zustände simulieren. Spielereignisse brechen den Farbtest ab. Es werden keine Spieleingaben durch das Addon ausgelöst.

Bei einem unerwarteten Zustand `/fishhelper trace` starten, einmal manuell angeln und `/fishhelper status` ausführen. Für die weitere Prüfung sind Build, Interface, Kanal-ID und beobachtete Zustandswechsel relevant. Lua-Fehler lassen sich mit `/console scriptErrors 1` sichtbar machen.

Die normale WoW-Oberfläche muss sichtbar bleiben. Bei ausgeblendeter UI (`Alt+Z`) ist auch die Fläche unsichtbar; Fish vorher stoppen. Andere Addons, UI-Skalierung, HDR/Farbverwaltung oder eine ungewöhnliche Darstellung können die aufgenommenen Bildschirmfarben beeinflussen und müssen im echten Client geprüft werden.

## Tests und aktueller Nachweis

Aus dem Projektstamm mit Lua 5.1 oder neuer:

```powershell
lua Tests/FishHelper.Tests.lua
```

Oder mit dem PowerShell-Runner und einem eigenen Interpreter:

```powershell
.\Tests\Test-FishHelper.ps1 -LuaPath 'C:\Tools\Lua\lua.exe'
```

Für eine lokale Node-basierte Testlaufzeit, ausschließlich unter dem ignorierten `artifacts`-Ordner:

```powershell
npm.cmd install --ignore-scripts --no-package-lock --no-save --prefix artifacts/lua-test-runtime fengari-node-cli@0.1.0
.\Tests\Test-FishHelper.ps1
```

Falls PowerShell lokale Skripte wegen der Ausführungsrichtlinie blockiert, den Runner mit einer nur für diesen Prozess geltenden Einstellung starten:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\Test-FishHelper.ps1
```

Diese Testlaufzeit gehört nicht in die WoW-Installation. Die Tests prüfen Zustände, Prioritäten, Loot-Haltezeit, doppelte Events, Timer-Abbruch, Inventarfehler, Restriktionswechsel und Diagnosebefehle mit simulierten WoW-APIs. Sie ersetzen keine echte Prüfung des Secret-/Taint-Systems oder der Bildschirmfarben.

Lokaler Prüfstand vom 14. November 2026: **33 Lua-Tests mit 669 Assertions** erfolgreich (Fengari); außerdem **74 vorhandene .NET-Prüfungen** erfolgreich. Die beiden Laufzeitdateien umfassen zusammen rund **13 KiB**; CPU- und Speicherverbrauch im Spiel sind noch nicht gemessen.

**Noch nicht im WoW-Client getestet.** Vor dem Einsatz einmal manuell prüfen: alle Angelränge des Charakters, Auto-Loot an/aus, Wurfabbruch, Inventarfehler, Tod/Wiederbelebung, Kampf, Reload und UI-/Auflösungswechsel. In Fish zusätzlich 150-ms- und 1000-ms-Abfrage testen. Das Ein-Pixel-Protokoll besitzt keine Quittierung und garantiert bei beliebig langen Desktop-Pausen keine verlustfreie Loot-Zählung.

## API-Grundlage

Recherche vom 14. November 2026, siehe [Plan](../../docs/wow-forever-addon-plan.md). Wesentliche Referenzen aus dem Forever-UI-Quellcode:

- [Kanal-/Casting- und Lebensstatus-API](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/UnitDocumentation.lua)
- [Secret-Value-Prüfungen](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/FrameScriptDocumentation.lua)
- [Restriktions-API](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/RestrictedActionsDocumentation.lua) und [Zustände](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/RestrictedActionsConstantsDocumentation.lua)
- [Loot-Events](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/LootDocumentation.lua) und [Taschen-API](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/ContainerDocumentation.lua)
