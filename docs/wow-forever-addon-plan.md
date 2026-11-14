# WeakAura durch ein kleines WoW-Forever-Addon ersetzen

Recherche und Plan vom **14. November 2026**. Quellen und Fish-Code geprüft.

Umsetzungsnachtrag: Das Addon heißt **FishHelper** und liegt mit Anleitung unter [`addon/FishHelper`](../addon/FishHelper/README.md). Die folgenden Abschnitte dokumentieren den ursprünglichen Entwurf; der Nachweis im echten WoW-Client steht weiterhin aus.

## Ergebnis

**Ja, der von Fish benötigte Statusgeber lässt sich voraussichtlich als sehr kleines, eigenständiges Addon umsetzen.** Fish benötigt fünf Zustände und eine farbige Fläche. Dafür ist keine allgemeine Aura-, Trigger- oder Konfigurationsengine erforderlich.

Die entscheidende offene Voraussetzung ist ein Test im tatsächlich verwendeten Forever-Client: Lassen sich der eigene Angelkanal und seine Spell-ID im gewünschten Spielkontext auswerten? Die API existiert, kann aber geschützte Werte liefern. Ein eigenes Addon hebt diese Beschränkung nicht auf. Bei unlesbaren Daten soll es Schwarz für „unbekannt/pausiert“ ausgeben.

Die Machbarkeit betrifft den Statusgeber. Ein vollständiger Ersatz der Desktop-Anwendung durch ein Addon folgt daraus nicht: Geschützte Spielaktionen unterliegen weiterhin dem Secure-Action-System. Für Bobberposition und Biss wurde in den geprüften Quellen keine belastbare, allgemein verwendbare API gefunden. Ein Forever-Addon-Autor untersucht diese Frage ebenfalls noch über Event-Tracing. [Blizzard zur Addon-Architektur](https://worldofwarcraft.blizzard.com/en-us/news/24246290/combat-philosophy-and-addon-disarmament-in-midnight), [Secure-Action-Quellcode](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_FrameXML/SecureTemplates.lua), [Angel-Event-Diagnose eines Forever-Addons](https://github.com/moust4ki/EasyControllerWowForever/blob/main/Vibration.lua)

## Was im Projekt tatsächlich benötigt wird

Die vorhandene Schnittstelle steht in [Detection.cs](../Core/Detection.cs). [BotEngine.cs](../Core/BotEngine.cs) liest den ersten Pixel der WoW-Clientfläche. [BotDesktop.cs](../Services/BotDesktop.cs) ermittelt diese Fläche ohne Fensterrahmen.

| Signal | RGB | Bedeutung für Fish | Geplante Datenquelle |
| --- | --- | --- | --- |
| Bereit | `#FF0000` | Neuer Wurf möglich | Nach vollständiger Initialisierung aus den übrigen Zuständen ableiten |
| Angeln | `#0000FF` | Bobber-/Splash-Suche läuft | Eigener Angelkanal, Kanal-Start/-Stop und Abgleich mit `UnitChannelInfo("player")` |
| Loot | `#00FF00` | Eine Loot-Bestätigung, keine Fisch-Stückzahl | Loot-Transaktion im Zusammenhang mit dem vorherigen Angelkanal |
| Inventar voll | `#00FFFF` | Sitzung beenden oder eingestellte Folgeaktion | Bestätigter Inventarfehler, ergänzend Taschenzustand |
| Tot/Geist | `#FFA500` | Sitzung beenden oder eingestellte Folgeaktion | `UnitIsDeadOrGhost("player")` und Lebenszyklus-Events |
| Unbekannt/gesperrt | `#000000` | Keine unmittelbare Wurf-/Loot-Aktion | Initialisierung, unlesbare Daten, nicht unterstützter Kontext |

Weiß wird ebenfalls als „bereit“ akzeptiert; das neue Addon sollte ausschließlich Rot dafür erzeugen. Unbekannte Farben werden bereits als `WANotFound` behandelt. Schwarz verhindert unmittelbare Spieleingaben, setzt aber den bestehenden Inaktivitäts-Countdown der Engine nicht außer Kraft. Die Pixel-Schnittstelle besitzt keine Versionskennung oder Rückmeldung vom Desktop-Programm.

Der originale WeakAura-Export ist im Repository nicht enthalten. Rekonstruierbar sind deshalb die von Fish erwarteten Farben und die dokumentierte Bedeutung, aber keine exakte Gleichheit aller alten Trigger, Prioritäten und Zeitintervalle.

## Aktuelle API-Grundlage und Addon-Entwicklung

Forever besitzt eine moderne WoW-API; die alte Classic-API darf nicht pauschal vorausgesetzt werden. Als konkreter Versionsnachweis liegt ein aus dem Client exportierter Stand **1.60.1, Build 70790, Interface 16001 vom 5. November 2026** vor. Das ist ein überprüfbarer Snapshot, keine Behauptung über den neuesten Live-Hotfix am Recherchetag. Vor der Umsetzung wird die Zielversion mit `GetBuildInfo()` aufgenommen. [Client-Export mit Versionsmetadaten](https://github.com/Atraeau/WoW-Addons/blob/main/docs/api.json), [Build-API aus dem Forever-UI-Quellcode](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/BuildDocumentation.lua)

Ein Addon besteht für unseren Zweck aus einer `.toc`-Datei für Metadaten/Ladereihenfolge und einer Lua-Datei. Lua läuft innerhalb des Spiels; Darstellung und Ereignisse kommen aus WoWs UI-API. Ein eigenes XML-Layout oder eine zusätzliche Laufzeit ist dafür nicht nötig. Die Blizzard-UI selbst verwendet dieses Ladeprinzip. [Blizzards TOC-Beispiel](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/Blizzard_APIDocumentationGenerated.toc)

Geplanter Aufbau:

```text
addon/FishHelper/
  FishHelper.toc
  FishHelper.lua
```

Beispiel für die Metadaten, vor Auslieferung an den Zielclient anzupassen:

```text
## Interface: 16001
## Title: FishHelper
## Version: 0.1.0
## Notes: Minimaler Angel-Statusgeber
FishHelper.lua
```

Der Ordner kommt in `Interface/AddOns` der **tatsächlich ausgewählten Forever-Installation**. Den Installationspfad aus Battle.net übernehmen. Lua-Änderungen mit `/reload` laden; neue Dateien und die erste Installation gegebenenfalls durch einen vollständigen Clientneustart einlesen. Entwicklung mit sichtbaren Lua-Fehlern und zeitlich begrenztem Event-Tracing, danach Diagnoseausgaben wieder deaktivieren.

Der normale Aufbau ist `CreateFrame` → benötigte Events registrieren → `OnEvent`-Handler → Zustand berechnen → vorhandene Farbtextur nur bei Änderung aktualisieren. `SetColorTexture` liefert die einfarbige Darstellung. [Textur-API](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/SimpleTextureBaseAPIDocumentation.lua), [laufendes Forever-Addon als Entwicklungsbeispiel](https://github.com/moust4ki/EasyControllerWowForever/blob/main/Vibration.lua)

### Für uns relevante API-Bereiche

| Bereich | Geprüfte Oberfläche | Konsequenz |
| --- | --- | --- |
| Kanal und Tod | `UnitChannelInfo`, `UNIT_SPELLCAST_CHANNEL_START/STOP`, `UnitIsDeadOrGhost` | API vorhanden; Kanalwerte und Event-Payload können abhängig von Einschränkungen geheim sein |
| Zugriffsschutz | `issecretvalue`, `canaccessvalue`, `C_RestrictedActions`, `ADDON_RESTRICTION_STATE_CHANGED` | Lesbarkeit vor Vergleichen, Tabellenzugriffen und Diagnoseausgabe prüfen |
| Loot | `LOOT_READY`, `LOOT_OPENED`, `LOOT_CLOSED`, `LOOT_SLOT_CLEARED` | Transaktion und Entnahme unterscheiden; Doppelmeldungen zusammenfassen |
| Taschen | `C_Container.GetContainerNumFreeSlots`, `BAG_UPDATE_DELAYED` | Abfragen nur bei Bedarf; Taschenfamilien berücksichtigen |
| Fehler | `UI_ERROR_MESSAGE(errorType, message)` | Inventarfehler anhand der im Zielclient verfügbaren Kennung auswerten |

Quellen: [Unit-API und Event-Payloads](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/UnitDocumentation.lua), [Lesbarkeitsprüfungen](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/FrameScriptDocumentation.lua), [Restriktionszustände](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/RestrictedActionsDocumentation.lua), [Loot](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/LootDocumentation.lua), [Container](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/ContainerDocumentation.lua), [Fehlerereignisse](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/SystemDocumentation.lua).

`SecretArguments = AllowedWhenUntainted` ist keine Zusicherung, dass Rückgabewerte frei lesbar sind. `UnitChannelInfo` und die Kanal-Events tragen ausdrücklich `SecretWhenUnitSpellCastRestricted`. Combat-Lockdown und Secret Values sind unterschiedliche Mechanismen; eine reine Prüfung auf „außerhalb des Kampfes“ genügt deshalb nicht als Lesbarkeitsnachweis.

Angel-Spell-IDs müssen zum Client passen. Ein aktuelles Angel-Addon berücksichtigt sowohl klassische Ränge wie `7620`/`7731` als auch andere Angelzauber und schützt seine Zugriffe gegen Secret Values. Das belegt einen praktikablen Entwicklungsansatz, ersetzt aber keinen Test unseres Clients. Keine einzelne Retail-ID und keinen fest eingebauten deutschen Zaubernamen voraussetzen. [Kompatibilitätscode von AnglersTackleBox](https://github.com/Sharpened-Banana/AnglersTackleBox/blob/main/Compat.lua)

## Geplantes Verhalten

### Kleiner Ereignisautomat

Ein Frame, eine Farbtextur, wenige lokale Zustandsvariablen und nur begrenzte Einmal-Timer. Keine WeakAuras-/Ace3-Abhängigkeit, kein dauerhaftes `OnUpdate`, kein regelmäßiger Vollscan, keine Medien oder Einstellungsoberfläche. Für v1 keine SavedVariables; Diagnose nur per Slash-Befehl.

Zielgröße: wenige hundert Lua-Zeilen und höchstens einige zehn KiB Quelltext. Das sind Entwurfsziele, keine bereits gemessenen Laufzeitwerte. CPU und Speicher werden im Spiel gemessen.

Priorität bei konkurrierenden Signalen: **Tod → unbekannt/gesperrt → bestätigter Inventarfehler → Loot → Angelkanal → bereit**. Ein anderer laufender Zauber, Ladebildschirm oder unsicherer Übergang soll nicht versehentlich „bereit“ erzeugen. Beim Weltbetreten und nach Ende einer Einschränkung wird der tatsächliche Zustand neu abgefragt; alte Timer dürfen dabei keinen neueren Zustand überschreiben.

### Loot richtig behandeln

Für den ersten Entwurf bedeutet Grün: **Eine zum Angelvorgang gehörende Loot-Transaktion wurde geöffnet**. Das passt zur dokumentierten Transaktionszählung von Fish, bestätigt aber noch keinen erfolgreich im Inventar gelandeten Fisch. `LOOT_SLOT_CLEARED` kann zur Diagnose und später für eine ausdrücklich geänderte Erfolgsdefinition dienen. Eine solche Änderung muss auch Beschriftungen und Statistik berücksichtigen.

Loot nur einem zuvor erkannten Angelvorgang zuordnen. `LOOT_READY` bereitet den Kontext vor; `LOOT_OPENED` erzeugt höchstens ein grünes Signal pro Transaktion. Gewöhnlicher Beutezugriff darf keinen Angelerfolg erzeugen. Der Kontext bleibt nach Kanalende kurz erhalten, weil Kanalende und Loot-Events zeitversetzt eintreffen können. Während dieses Übergangs zunächst Schwarz statt eines voreiligen Rot anzeigen. Dauer und Event-Reihenfolge werden im Client ermittelt.

Fish tastet standardmäßig alle **150 ms**, einstellbar zwischen **100 und 1000 ms**, ab; zusätzlich warten einzelne Engine-Schritte. Daher Grün über `LOOT_CLOSED` hinaus kurz halten. Startwert für die Erprobung: **1,5 Sekunden**, anschließend unter Last und bei 1000-ms-Abfrage überprüfen. Ein kurzer Frame-Impuls ist unzureichend. Das Ein-Pixel-Protokoll kann bei beliebig langen Pausen keine verlustfreie Zustellung garantieren; eine echte Quittierung wäre eine separate Erweiterung.

### Inventar voll

Für v1 ist ein bestätigter „Inventar voll“-Fehler maßgeblich. Numerische Kennung im Zielclient prüfen; nötigenfalls gegen dessen lokalisierte Blizzard-Konstanten wie `ERR_INV_FULL` vergleichen, nur bei lesbaren Werten. Keine eigenen deutschen/englischen Fehlermeldungstexte fest einbauen. Die Fehler-API liefert Typ und Text. [Fehler-API](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/SystemDocumentation.lua)

Null freie Slots sind nur ein Indiz: Bestehende Stapel können noch Platz bieten, Spezialtaschen nehmen nicht jeden Gegenstand auf. Deshalb daraus allein keinen harten Stopp ableiten. Ein bestätigter Fehler bleibt gesetzt, bis freie nutzbare Kapazität nachgewiesen oder der Status ausdrücklich zurückgesetzt wird. Bei Start mit gefüllten Taschen kann damit erst der erste echte Fehlversuch einen sicheren Voll-Status liefern. Das ist die bewusst gewählte v1-Semantik; vorausschauende Kapazitätsberechnung wäre Zusatzumfang.

### Darstellung

Eine kleine, vollständig deckende Farbfläche, beispielsweise **4 × 4 physische Pixel**, direkt an der oberen linken Ecke des Spielinhalts. Der von Fish gelesene Pixel `(0,0)` muss vollständig darin liegen. Keine Rahmen, Farbverläufe, Animationen oder Mausinteraktion. Position und Größe unter UI-Skalierung, Auflösungswechsel und Fensterwechsel neu ausrichten. Exakte Bildschirmfarben über die vorhandene Aufnahme prüfen; UI-Koordinaten sind nicht automatisch physische Pixel.

## Umsetzungsschritte mit Abnahmekriterien

1. **Clientnachweis und Protokoll festhalten.** Version/Interface auslesen, einen manuellen Angelvorgang mit begrenztem Event-Trace untersuchen, Kanal-ID, Lesbarkeit, Stop-/Loot-Reihenfolge und Inventarfehler erfassen. Vorhandenen WeakAura-Export abgleichen, falls verfügbar; sonst die hier dokumentierte v1-Semantik zugrunde legen. Ergebnis: konkrete Messdaten und eine eindeutige Zustands-/Prioritätstabelle. Falls der Angelkanal im vorgesehenen Kontext nicht lesbar ist, keine vollständige Ablösung zusagen.

2. **Minimalen Statusgeber bauen.** Die zwei Dateien unter `addon/FishHelper` anlegen, Farbfläche und Ereignisautomaten umsetzen, Initialisierung/Restriktionen berücksichtigen. Slash-Befehl für Version, aktuellen Zustand und einen befristeten Farbtest ergänzen. Bei eingeschaltetem Farbtest ist die Desktop-Sitzung gestoppt. Ergebnis: Das Addon lädt allein ohne WeakAuras und zeigt manuelle Spielzustände korrekt an.

3. **Übergänge belastbar machen.** Loot-Haltezeit, Zuordnung und Entprellung, Inventarfehler sowie generationsgebundene Timer ergänzen. Bei Reload, Weltwechsel, Tod und Kanalwechsel alle überholten Übergänge verwerfen. Ergebnis: Kein falsches Rot und keine doppelten Loot-Bestätigungen in den geprüften Eventfolgen.

4. **Fish anbinden und dokumentieren.** Bestehende Farben und Koordinate beibehalten; sichtbare Texte von „WeakAura“ auf „Status-Addon“ umstellen. Interne Umbenennung von `WANotFound` nur bei erkennbarem Nutzen. README um Installation, Signalbedeutung und Diagnose ergänzen. Addon-Paket zunächst als separates ZIP mit Ordner `FishHelper/` vorsehen. Ergebnis: keine Änderung am Erkennungsalgorithmus für den Austausch erforderlich.

5. **Prüfen und Abnahme dokumentieren.** Relevante .NET-Tests ausführen, kompakte Lua-Tests für Zustandsübergänge mit gemockten Events ergänzen und eine echte Forever-Sitzung prüfen. Lua-Mocks können die Secret-/Taint-Regeln des Clients nicht nachweisen. Build, verwendete Einstellungen, Beobachtungen und Laufzeitmessung festhalten. Ergebnis: getesteter Statusgeber mit nachvollziehbarer Kompatibilitätsangabe.

Vor dem praktischen Einsatz ist die kritische API-Voraussetzung im Zielclient nachzuweisen. Der anschließend angelegte FishHelper-Prototyp ermöglicht diese Prüfung mit Diagnosebefehlen; eine Veröffentlichung ist nicht Bestandteil der bisherigen Arbeit.

### Wesentliche Testfälle

| Fall | Erwartung |
| --- | --- |
| Login/Reload ohne oder während eines Angelkanals | Erst Schwarz, dann korrekt rekonstruierter Zustand |
| Auswerfen, Abbruch durch Bewegung/Escape, ausgelaufener Wurf | Blau nur für tatsächlich erkannten Angelkanal; keine falsche Loot-Meldung |
| Andere Zauber sowie Kampf-/Restriktionswechsel | Kein Angelzustand aus fremdem Zauber; bei Unsicherheit Schwarz; danach Neuabgleich |
| Auto-Loot an/aus, schnelles Schließen, mehrere Loot-Events | Genau eine ausreichend lange grüne Phase pro passender Transaktion |
| Beute außerhalb eines Angelvorgangs | Keine Angel-Loot-Bestätigung |
| Volle Taschen, teilweise freie Stapel, Spezialtaschen, Platz freimachen | Voll-Status nur gemäß festgelegter Semantik; Rücknahme nachvollziehbar |
| Tod, Geist, Wiederbelebung | Orange hat Vorrang; anschließende Zustandsrekonstruktion |
| UI-Skalierung, Auflösung, Fenster-/Vollbildmodus, UI ausblenden | Pixelposition und RGB-Werte prüfen; Verhalten bei ausgeblendeter UI dokumentieren |
| Abfrage alle 150/1000 ms und Last | Grüne Phase wird erkannt; keine veralteten Timer schreiben neuere Zustände um |

Die vorhandenen Farbtests stehen in [Tests/Program.cs](../Tests/Program.cs). Für die Integration sind `dotnet run --project Tests/Fish.Tests.csproj` und bei Änderungen an der Aufnahme zusätzlich der bestehende Desktop-Smoke-Test vorgesehen. Beim ursprünglichen Dokumentationsstand wurden keine Anwendungstests ausgeführt; die Prüfung der späteren Implementierung ist in der FishHelper-Anleitung beschrieben.
