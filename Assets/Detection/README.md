# Schwimmer-Referenz

`bobber.json` ist die einzige Schwimmer-Referenz im Produktionsprogramm. Sie enthält Blau, Rot, Körper und Metallöse einschließlich Farbe, Toleranzen, Größe, Pixelmenge, relativer Position und Körper-Klickpunkt. Das PNG wird zur Laufzeit nicht benötigt.

Das Profil wird beim Laden validiert. Änderungen an der Referenz sind eine Änderung des Detektors und müssen gegen die Positiv- und Negativbilder unter `Tests/Fixtures/` geprüft werden.

Die Laufzeitsuche ergänzt das Profil durch lokale Hintergrundtrennung und Weißabgleich-Hypothesen. Zusätzliche Farbtoleranz gilt nur für die vom Wasser getrennten Objektteile. Größe, Lage, Flächendichte und die geschlossene Metallöse bleiben unabhängige Prüfungen; höhere Toleranz erklärt keine Wasserfläche automatisch zum Schwimmer.

Die ursprüngliche transparente PNG liegt ausschließlich unter `Tests/Fixtures/bobber-reference.png`. Vorbereitung und Vermessung befinden sich unter `Tests/Support/`. Ein neues Profil kann außerhalb der Anwendung erzeugt werden:

```powershell
dotnet run --project Tests/Fish.Tests.csproj -- --export-bobber-colors Assets/Detection/bobber.json
dotnet run --project Tests/Fish.Tests.csproj
```

Die gespeicherte Prüfsumme dokumentiert die Herkunft der Referenz. Sie löst im Produktionsprogramm kein erneutes Einlesen oder Vermessen einer PNG aus.
