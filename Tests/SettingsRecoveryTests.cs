using Fish.Configuration;
using System.Text.Json;

static class SettingsRecoveryTests
{
    public static void Run(Action<bool, string> check, string root)
    {
        string Case(string name) { var path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        string Json(int level) => JsonSerializer.Serialize(new BotOptions { FishingHotkey = "Ctrl+F8", BobberSensitivity = level,
            SplashSensitivity = 81, BobberAreaXOffset = level * 2 }, SettingsJsonContext.Default.BotOptions);
        bool Fails(Action action) { try { action(); return false; } catch { return true; } }

        string legacyFolder = Case("old-exe"), targetFolder = Case("local-data");
        string legacy = Path.Combine(legacyFolder, "appsettings.json"), target = Path.Combine(targetFolder, SettingsStore.FileName);
        string original = """{"SchemaVersion":6,"FishingHotkey":"Ctrl+F8","BobberSensitivity":72,"SplashSensitivity":81,"BobberAreaWidth":720,"BobberMatchMinimum":50,"BobberColorTolerance":50,"SplashPixelTolerance":50,"SplashChangePercent":10}""";
        File.WriteAllText(legacy, original);
        File.WriteAllText(legacy + ".bak", Json(31));
        var store = new SettingsStore(targetFolder, legacyFolder);
        var imported = store.Load();
        check(imported is { SchemaVersion: 7, FishingHotkey: "Ctrl+F8", BobberSensitivity: 72, SplashSensitivity: 81, BobberAreaWidth: 720 } && File.Exists(target),
            "Erster Start übernimmt die bisherigen Werte aus dem EXE-Ordner nach settings.json");
        check(File.ReadAllText(legacy) == original && File.ReadAllText(legacy + ".bak") == Json(31),
            "Migration lässt die alte Datei und deren Sicherung unverändert");
        check(!File.Exists(Path.Combine(targetFolder, "appsettings.json")), "Am neuen Ort wird ausschließlich settings.json angelegt");
        File.WriteAllText(legacy, Json(15));
        check(store.Load().BobberSensitivity == 72, "Bestehende neue Einstellungen haben Vorrang vor späteren Änderungen der alten Datei");

        string portableFolder = Case("portable");
        File.WriteAllText(Path.Combine(portableFolder, "appsettings.json"), original);
        check(new SettingsStore(portableFolder).Load().BobberSensitivity == 72 &&
            File.Exists(Path.Combine(portableFolder, SettingsStore.FileName)), "Expliziter Speicherordner migriert nur seine eigenen alten Einstellungen");
        string emptyFolder = Case("isolated-empty");
        check(new SettingsStore(emptyFolder).Load().FishingHotkey == "1" && Directory.GetFiles(emptyFolder).Length == 0,
            "Leere Testordner importieren keine persönlichen Einstellungen und schreiben beim bloßen Lesen keine Defaults");

        store.Save(new() { BobberSensitivity = 63 });
        store.Save(new() { BobberSensitivity = 84 });
        string backup = File.ReadAllText(target + ".bak");
        File.WriteAllText(target, "{kaputt");
        check(store.Load().BobberSensitivity == 63 && File.ReadAllText(target + ".bak") == backup,
            "Beschädigte Hauptdatei wird aus gültiger Sicherung geladen, ohne die Sicherung zu überschreiben");
        check(JsonDocument.Parse(File.ReadAllText(target)).RootElement.GetProperty("BobberSensitivity").GetInt32() == 63 &&
            Directory.GetFiles(targetFolder, "settings.json.corrupt-*").Any(path => File.ReadAllText(path) == "{kaputt"),
            "Wiederherstellung repariert die Hauptdatei und bewahrt die ursprünglichen beschädigten Daten auf");
        store.Save(new() { BobberSensitivity = 77 });
        File.WriteAllText(target, "");
        check(store.Load().BobberSensitivity == 63, "Auch eine leere Hauptdatei wird sicher wiederhergestellt");
        File.Delete(target);
        check(store.Load().BobberSensitivity == 63 && File.Exists(target), "Vorhandene Sicherung stellt auch eine fehlende Hauptdatei wieder her");

        File.WriteAllText(target, "{kaputt"); File.WriteAllText(target + ".bak", "ebenfalls kaputt");
        check(Fails(() => store.Load()) && File.ReadAllText(target) == "{kaputt" && File.ReadAllText(target + ".bak") == "ebenfalls kaputt",
            "Zwei beschädigte Dateien bleiben erhalten; alte EXE-Einstellungen werden nicht stillschweigend importiert");
        File.WriteAllText(target, """{"SchemaVersion":999}"""); File.WriteAllText(target + ".bak", Json(63));
        check(Fails(() => store.Load()) && Fails(() => store.Save(new())) && File.ReadAllText(target).Contains("999") &&
            File.ReadAllText(target + ".bak") == Json(63), "Neuere Formate blockieren Laden und Speichern statt auf eine ältere Sicherung zurückzufallen");
        check(Directory.GetFiles(targetFolder, "*.tmp").Length == 0, "Fehlgeschlagene Schreibvorgänge hinterlassen keine temporären Dateien");
        File.WriteAllText(target, "{kaputt"); File.WriteAllText(target + ".bak", """{"SchemaVersion":999}""");
        check(Fails(() => store.Load()) && File.ReadAllText(target) == "{kaputt", "Eine Sicherung aus einer neueren Version wird ebenfalls nicht eingespielt");

        string recoveryLegacyFolder = Case("legacy-backup"), recoveryTargetFolder = Case("legacy-recovered");
        string recoveryLegacy = Path.Combine(recoveryLegacyFolder, "appsettings.json");
        File.WriteAllText(recoveryLegacy, "{kaputt"); File.WriteAllText(recoveryLegacy + ".bak", Json(57));
        check(new SettingsStore(recoveryTargetFolder, recoveryLegacyFolder).Load().BobberSensitivity == 57 && File.ReadAllText(recoveryLegacy) == "{kaputt",
            "Migration kann eine gültige alte Sicherung übernehmen, ohne die Quelle zu verändern");

        string concurrentFolder = Case("concurrent");
        Parallel.For(0, 24, i => new SettingsStore(concurrentFolder).Save(new() { BobberSensitivity = i, BobberAreaXOffset = i * 2 }));
        var concurrent = new SettingsStore(concurrentFolder).Load();
        using var previous = JsonDocument.Parse(File.ReadAllText(Path.Combine(concurrentFolder, SettingsStore.FileName) + ".bak"));
        check(concurrent.BobberAreaXOffset == concurrent.BobberSensitivity * 2 &&
            previous.RootElement.GetProperty("BobberAreaXOffset").GetInt32() == previous.RootElement.GetProperty("BobberSensitivity").GetInt32() * 2 &&
            Directory.GetFiles(concurrentFolder, "*.tmp").Length == 0,
            "Parallele Store-Instanzen erhalten vollständige Haupt- und Sicherungsdateien ohne vermischte Zustände");
    }
}
