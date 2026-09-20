using Fish.Configuration;
using Fish.Core;
using Fish.Native;
using Fish.Services;

int passed = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
var previewBuffer = new PreviewBuffer();
var previewPixels = new int[800 * 400];
previewPixels[0] = 0xff0000;
previewBuffer.Add(new(800, 400, previewPixels), false, true);
previewPixels[0] = 0;
var preview = previewBuffer.Take();
Check(preview.Bobber is { Width: 480, Height: 240 } && preview.Bobber.Pixels[0] == unchecked((int)0xffff0000), "Vorschau bewahrt Seitenverhältnis, kopiert Pixel und setzt Alpha");
previewBuffer.Add(new(2, 1, [0xffffff, 0]), true, true);
Check(previewBuffer.Take().Splash?.Pixels[0] == unchecked((int)0xffffffff), "Splash-Fund bleibt unabhängig vom Live-Limit erhalten");
Check(previewBuffer.Take() == (null, null, null), "Vorschaupuffer gibt konsumierte Bilder frei");
var colors = new (int Pixel, BotState State)[] { (0xff0000, BotState.Idle), (0x0000ff, BotState.Fishing), (0x00ff00, BotState.Looting),
    (0xffffff, BotState.Idle), (0x000000, BotState.WANotFound), (0xffff00, BotState.WANotFound),
    (0xff00ff, BotState.WANotFound), (0x00ffff, BotState.FullBag), (0xffa500, BotState.Dead), (0x123456, BotState.WANotFound) };
foreach (var (color, state) in colors) Check(Detection.State(color | unchecked((int)0xff000000)) == state, "WeakAura " + state);
Check(Detection.Find(new(2, 1, [0x111111, 0xff0000]), 0, 0, false) == new PixelPoint(1, 0), "Rote Feder");
Check(Detection.Find(new(2, 1, [0x111111, 0xff0000]), 1, 0, false) == null, "Scan-Schrittweite");
Check(Detection.Find(new(1, 1, [0x0000ff]), 0, 0, false) == null, "Blaue Feder wird ignoriert");
Check(Detection.Find(new(1, 1, [0x826e50]), 0, 0, false) == null, "Brauner Körper wird ignoriert");
Check(Detection.Find(new(1, 1, [0xe6e6e6]), 0, 20, true) != null, "Splash Toleranzgrenze inklusiv");
Check(Detection.Find(new(1, 1, [0xe5e6e6]), 0, 20, true) == null, "Splash außerhalb Toleranz");
var monitor = new MonitorInfo("TEST", new(-1920, -200, 1920, 1080));
Check(Detection.Around(new(-960, 340), 180, 120, 0, 20, monitor.Bounds) == new PixelRect(-1050, 260, 180, 120), "Negative Monitorkoordinaten und Y-Versatz");
Check(Detection.Around(new(-1910, -190), 100, 100, 0, 0, monitor.Bounds) == new PixelRect(-1920, -200, 60, 60), "Suchbereich am Monitorrand beschneiden");

string folder = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "tests", Guid.NewGuid().ToString("N"));
var store = new SettingsStore(folder);
var defaults = store.Load();
Check(defaults.PerformanceSkip == 2, "Standard: zwei Pixel überspringen");
Check(defaults.BobberAreaWidth == 800 && defaults.BobberAreaHeight == 400 && defaults.BobberAreaYOffset == 30, "Standard: Bobber-Geometrie");
Check(defaults.BobberAreaTolerance == 200 && defaults.BobberAreaAutoLearn, "Standard: Bobber-Toleranz und Auto-Learn");
Check(defaults.SplashAreaTolerance == 70 && defaults.SplashAreaAutoLearn, "Standard: Splash-Toleranz und Auto-Learn");
store.Save(new BotOptions { BobberAreaTolerance = 400, PerformanceScanRate = 0 });
var loaded = store.Load();
Check(loaded.BobberAreaTolerance == 255 && loaded.PerformanceScanRate == 100, "Einstellungen normalisieren");
store.Save(new BotOptions { FeaturesCastTime = 12, BobberAreaWidth = 600 });
Check(File.Exists(Path.Combine(folder, "appsettings.json.bak")) && store.Load().FeaturesCastTime == 12, "Atomisches Speichern mit Backup");
Check(store.Load().BobberAreaWidth == 600, "Gespeicherte Benutzerwerte bleiben erhalten");
File.WriteAllText(Path.Combine(folder, "appsettings.json"), "{\"PerformanceScanRate\":150,}");
Check(store.Load().BobberAreaWidth == 800 && store.Load().SplashAreaTolerance == 70, "Fehlende Felder erhalten neue Defaults");
Check(new SettingsStore().Folder == Path.GetFullPath(SettingsStore.DefaultFolder), "Standard-Speicherort ist das Anwendungsverzeichnis");
var changed = new BotOptions { 
    WindowWidth = 1100, WindowHeight = 800, WindowXPosition = 120, WindowYPosition = 90,
    PerformanceSkip = 4, BobberAreaTolerance = 180, SplashAreaTolerance = 65, FeaturesExitOnDeath = true };
store.Save(changed);
var restarted = new SettingsStore(folder).Load();
Check(
    restarted.WindowWidth == 1100 && restarted.WindowHeight == 800 && restarted.WindowXPosition == 120 && restarted.WindowYPosition == 90 &&
    restarted.PerformanceSkip == 4 && restarted.BobberAreaTolerance == 180 && restarted.SplashAreaTolerance == 65 && restarted.FeaturesExitOnDeath,
    "Neustart lädt Fenster, Steuerung, Erkennung und Aktionen");
async Task RunUntilCanceled(BotEngine engine, BotOptions config, CancellationTokenSource cts)
{
    try { await engine.RunAsync(config, cts.Token); }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
{
    var fake = new FakeDesktop();
    var engine = new BotEngine(fake);
    fake.OnKey = key => { if (key == 0x31) cts.Cancel(); };
    await RunUntilCanceled(engine, new(), cts);
    Check(fake.Keys.SequenceEqual([0x31]) && fake.Loots == 0, "Stop während Cast verhindert Folgeaktionen");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
{
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x0000ff, OnCapture = () => clock.Advance(1) };
    var engine = new BotEngine(fake, clock);
    fake.OnKey = key => { if (key == 0x1b) cts.Cancel(); };
    await RunUntilCanceled(engine, new() { FeaturesCastTime = 5, PerformanceScanRate = 100, BobberAreaAutoLearn = false, SplashAreaAutoLearn = false }, cts);
    Check(fake.Keys.SequenceEqual([0x1b]), "Cast-Timeout auch ohne Bobber und ohne Auto-Learn");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    var fake = new FakeDesktop { StatePixel = 0xffa500 };
    var engine = new BotEngine(fake);
    engine.Updated += update => { if (update.Activity.Contains("Stop zum Abbrechen")) cts.Cancel(); };
    await RunUntilCanceled(engine, new() { FeaturesExitOnDeath = true, FeaturesShutdownOnDeath = true }, cts);
    Check(fake.Closes == 0 && fake.Shutdowns == 0 && fake.Keys.Count == 0, "Stop bricht Exit und Shutdown ab");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    var fake = new FakeDesktop();
    var engine = new BotEngine(fake);
    fake.OnKey = _ => cts.Cancel();
    await RunUntilCanceled(engine, new(), cts);
    Check(fake.Keys.SequenceEqual([0x31]), "Verfügbares WoW startet mit Taste 1");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x0000ff, OnCapture = () => clock.Advance(1), FindBobber = true, FindSplash = true };
    fake.OnLoot = () => cts.Cancel();
    var engine = new BotEngine(fake, clock);
    await RunUntilCanceled(engine, new() { PerformanceScanRate = 100 }, cts);
    Check(fake.Loots == 1 && fake.LastLoot is { X: < 0 }, "Bobber zu Splash zu Loot mit negativen Koordinaten");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x00ffff };
    var engine = new BotEngine(fake, clock);
    engine.Updated += update => { if (update.Activity.Contains("Stop zum Abbrechen")) clock.Advance(61); };
    await RunUntilCanceled(engine, new() { FeaturesExitOnFullBag = true, FeaturesShutdownOnFullBag = true }, cts);
    Check(fake.Closes == 1 && fake.Shutdowns == 1 && fake.Keys.Count == 0, "Exit und Shutdown nach simulierten Deadlines genau einmal");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    var fake = new FakeDesktop { WindowAvailable = false };
    var engine = new BotEngine(fake);
    engine.Updated += update => { if (update.State == BotState.WoWNotActive) cts.Cancel(); };
    await RunUntilCanceled(engine, new(), cts);
    Check(fake.Keys.Count == 0 && fake.Captures == 0, "Minimiertes Fenster pausiert ohne Aufnahme oder Eingaben");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
{
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x0000ff, FindBobber = true, FindSplash = true };
    var moved = new PixelRect(120, 80, 1600, 900);
    fake.OnCapture = () => { clock.Advance(1); if (fake.Captures == 2) fake.Bounds = moved; };
    fake.OnLoot = () => cts.Cancel();
    int bobberHits = 0;
    var engine = new BotEngine(fake, clock);
    engine.Logged += message => { if (message.EventId == "bobber.found") bobberHits++; };
    await RunUntilCanceled(engine, new() { PerformanceScanRate = 100 }, cts);
    Check(fake.Loots == 1 && fake.LastExpectedBounds == moved && bobberHits == 1,
        "Fensterbewegung während Aufnahme verwirft veralteten Treffer");
    Check(fake.CapturedAreas.Contains(new(moved.X, moved.Y, 1, 1)), "WeakAura-Pixel folgt der Spielfläche");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
{
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x0000ff, FindBobber = true, FindSplash = true, OnCapture = () => clock.Advance(1) };
    var resized = new PixelRect(-1500, 120, 1400, 800);
    int bobberHits = 0;
    var engine = new BotEngine(fake, clock);
    engine.Logged += message =>
    {
        if (message.EventId == "bobber.found" && ++bobberHits == 1) fake.Bounds = resized;
    };
    fake.OnLoot = () => cts.Cancel();
    await RunUntilCanceled(engine, new() { PerformanceScanRate = 100 }, cts);
    Check(bobberHits == 2 && fake.Loots == 1 && fake.LastExpectedBounds == resized,
        "Größenänderung verwirft Splash-Bereich und sucht Bobber erneut");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    var fake = new FakeDesktop { StatePixel = 0x123456 };
    fake.OnCapture = () => { if (fake.Captures == 5) cts.Cancel(); };
    var updates = new List<BotUpdate>();
    var engine = new BotEngine(fake);
    engine.Updated += updates.Add;
    await RunUntilCanceled(engine, new() { PerformanceScanRate = 100 }, cts);
    Check(fake.Captures == 5 && updates.Count == 2 && updates[^1].Activity == "Gestoppt" && fake.Keys.Count == 0,
        "Unveränderter Status wird einmal gemeldet; Stop bleibt sichtbar");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(350)))
{
    var fake = new FakeDesktop { StatePixel = 0x0000ff };
    fake.OnCapture = () => fake.Bounds = fake.Bounds with { X = fake.Bounds.X + 1 };
    await RunUntilCanceled(new BotEngine(fake), new() { PerformanceScanRate = 100 }, cts);
    Check(fake.Captures is > 0 and <= 5 && fake.Keys.Count == 0 && fake.Loots == 0,
        "Ständige Fensterbewegung respektiert das Scan-Intervall");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
{
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x0000ff, OnCapture = () => clock.Advance(1) };
    fake.OnKey = key =>
    {
        if (key == 0x1b) { fake.Game = 2; fake.FindBobber = fake.FindSplash = true; }
    };
    fake.OnLoot = () => cts.Cancel();
    await RunUntilCanceled(new BotEngine(fake, clock), new()
    {
        FeaturesCastTime = 5, PerformanceScanRate = 100, BobberAreaAutoLearn = false, SplashAreaAutoLearn = false
    }, cts);
    Check(fake.Keys.SequenceEqual([0x1b]) && fake.Loots == 1,
        "Neues WoW-Fenster verwirft alten Timeout und erkennt wieder Bobber und Splash");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
{
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x0000ff, FindBobber = true, FindSplash = true, OnCapture = () => clock.Advance(1) };
    var entries = new List<BotLogEntry>();
    var engine = new BotEngine(fake, clock);
    engine.Logged += entries.Add;
    fake.OnLoot = () => fake.StatePixel = 0x00ff00;
    engine.Updated += update => { if (update.State == BotState.Looting) cts.Cancel(); };
    await RunUntilCanceled(engine, new() { PerformanceScanRate = 100 }, cts);
    int request = entries.FindIndex(e => e.EventId == "loot.requested");
    int confirmed = entries.FindIndex(e => e.EventId == "loot.confirmed");
    Check(request >= 0 && confirmed > request && entries[confirmed].Level == LogLevel.Success,
        "Protokoll trennt Loot-Anfrage von späterer WeakAura-Bestätigung");
    Check(entries[^1].EventId == "session.ended" && entries[^1].Message.Contains("1 Loot-Anfragen") && entries[^1].Message.Contains("1 Loot-Bestätigungen"),
        "Sitzungsbilanz enthält tatsächliche Loot-Ereignisse");
    Check(entries[0].Timestamp == new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) && entries.All(e => !string.IsNullOrWhiteSpace(e.Category)),
        "Log-Zeitstempel entsteht beim Ereignis und nutzt die Engine-Uhr");
}
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    var entries = new List<BotLogEntry>();
    var engine = new BotEngine(new FakeDesktop { StatePixel = 0xffa500 });
    engine.Logged += entries.Add;
    engine.Updated += update => { if (update.Activity.Contains("Stop zum Abbrechen")) cts.Cancel(); };
    await RunUntilCanceled(engine, new() { FeaturesExitOnDeath = true, FeaturesShutdownOnDeath = true }, cts);
    Check(entries.Count(e => e.EventId == "action.scheduled" && e.Level == LogLevel.Warning) == 1 &&
        entries.Count(e => e.EventId == "action.cancelled") == 1 && !entries.Any(e => e.EventId is "action.close" or "action.shutdown"),
        "Protokoll dokumentiert Countdown und Abbruch ohne ausgeführte Aktion zu behaupten");
}
var logModel = new Fish.ViewModels.MainViewModel();
for (int i = 0; i < 305; i++) logModel.AddLog($"Eintrag {i}");
Check(logModel.Log.Count == 300 && logModel.Log[0].Message == "Eintrag 304" && logModel.Log[^1].Message == "Eintrag 5",
    "Protokoll behält die neuesten 300 Einträge");
logModel.AddLog("Testfehler", LogLevel.Error);
Check(logModel.Log[0].Level == LogLevel.Error && logModel.Log[0].ToString().Contains("[FEHLER]"),
    "Fehler sind in Anzeige und Text-Export gekennzeichnet");
var metricsStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var metrics = new SessionMetrics();
void MetricEvent(string id, int seconds) => metrics.Record(new(metricsStart.AddSeconds(seconds), LogLevel.Info, "Test", id, ""));
MetricEvent("session.started", 0);
Check(metrics.Snapshot(metricsStart) is { CastsPerMinute: 0, LootsPerMinute: 0, SuccessPercent: null, ScanMilliseconds: null },
    "Performance ohne Daten zeigt keine erfundenen Messwerte");
MetricEvent("cast.requested", 10);
MetricEvent("loot.confirmed", 20);
MetricEvent("loot.confirmed", 21);
MetricEvent("cast.requested", 25);
metrics.RecordScan(metricsStart.AddSeconds(28), 10);
metrics.RecordScan(metricsStart.AddSeconds(29), 30);
var halfMinute = metrics.Snapshot(metricsStart.AddSeconds(30));
Check(halfMinute.CastsPerMinute == 4 && halfMinute.LootsPerMinute == 4 && halfMinute.SuccessPercent == 50 && halfMinute.AverageCatchSeconds == 10,
    "Performance rechnet erste Minute hoch und zählt höchstens einen Erfolg pro eigenem Wurf");
Check(halfMinute.ScanMilliseconds == 20 && metrics.Snapshot(metricsStart.AddSeconds(35)).ScanMilliseconds == null,
    "Scan-Mittelwert berücksichtigt nur Messungen der letzten fünf Sekunden");
MetricEvent("cast.timeout", 40);
var afterMinute = metrics.Snapshot(metricsStart.AddSeconds(80));
Check(afterMinute.CastsPerMinute == 1 && afterMinute.LootsPerMinute == 1 && afterMinute.Casts == 2 && afterMinute.Loots == 2 && afterMinute.Timeouts == 1,
    "Rollierende Minutenraten verwerfen alte Ereignisse und erhalten Gesamtzahlen");
MetricEvent("session.ended", 80);
Check(metrics.Snapshot(metricsStart.AddHours(1)) == afterMinute with { Running = false },
    "Performance-Werte bleiben nach Stop eingefroren");
MetricEvent("session.started", 90);
Check(metrics.Snapshot(metricsStart.AddSeconds(91)) is { Casts: 0, Loots: 0, Timeouts: 0, SuccessPercent: null },
    "Neue Sitzung setzt alle Performance-Zähler zurück");
MetricEvent("loot.confirmed", 92);
Check(metrics.Snapshot(metricsStart.AddSeconds(93)) is { Loots: 1, SuccessfulCasts: 0, AverageCatchSeconds: null },
    "Übernommener Wurf verfälscht weder Erfolgsquote noch eigene Wurfdauer");
foreach (string outcome in new[] { "empty", "loot", "timeout", "cancel" })
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var clock = new TestClock();
    var fake = new FakeDesktop { StatePixel = 0x0000ff, OnCapture = () => clock.Advance(1), FindBobber = true, FindSplash = true };
    var engine = new BotEngine(fake, clock);
    var counts = new SessionMetrics();
    engine.Logged += entry =>
    {
        counts.Record(entry);
        if (entry.EventId == "loot.confirmed") fake.StatePixel = 0xff0000;
    };
    fake.OnLoot = () =>
    {
        if (outcome == "cancel") cts.Cancel();
        else if (outcome == "timeout") clock.Advance(30);
        else fake.StatePixel = outcome == "loot" ? 0x00ff00 : 0xff0000;
    };
    fake.OnKey = key => { if (key == 0x31) cts.Cancel(); else if (key == 0x1b) fake.StatePixel = 0xff0000; };
    await RunUntilCanceled(engine, new() { PerformanceScanRate = 100 }, cts);
    var result = counts.Snapshot(clock.GetUtcNow());
    Check(fake.Loots == 1 && result.NoFish == (outcome == "empty" ? 1 : 0) && result.Timeouts == (outcome == "timeout" ? 1 : 0),
        "Kein Fisch angebissen: abgeschlossenes Splash-Ergebnis " + outcome);
    counts.Record(new(clock.GetUtcNow(), LogLevel.Info, "Sitzung", "session.started", ""));
    Check(counts.Snapshot(clock.GetUtcNow()).NoFish == 0, "Neue Sitzung setzt Kein-Fisch-Zähler zurück: " + outcome);
}
var release = UpdateService.ParseRelease("""{"draft":false,"prerelease":false,"tag_name":"v1.2.3","html_url":"https://github.com/leafcreek/Fish/releases/tag/v1.2.3","body":"Test notes"}""");
Check(release.Version == new Version(1, 2, 3, 0), "Updateprüfung liest stabile Version");
bool invalidRelease = false;
try { UpdateService.ParseRelease("""{"draft":false,"prerelease":false,"tag_name":"v1.2.3","html_url":"file:///bad","body":""}"""); }
catch (InvalidDataException) { invalidRelease = true; }
Check(invalidRelease, "Updateprüfung verweigert unsichere Download-Adressen");
var preferences = new BotOptions { CloseToTray = true };
store.Save(preferences);
var savedPreferences = store.Load();
Check(savedPreferences.CloseToTray,
    "Anwendungseinstellungen überstehen erneutes Laden");
var mainWork = new PixelRect(0, 0, 1920, 1040);
var leftWork = new PixelRect(-1920, -100, 1920, 1040);
Check(WindowPlacement.Fit(new(-1800, -50, 760, 740), [mainWork, leftWork], mainWork) == new PixelRect(-1800, -50, 760, 740), "Fensterposition auf Monitor mit negativen Koordinaten bleibt erhalten");
var recovered = WindowPlacement.Fit(new(6000, 200, 760, 740), [mainWork], mainWork);
Check(recovered == new PixelRect(580, 150, 760, 740), "Entfernter Monitor führt zu sichtbarem zentriertem Fenster");
Check(WindowPlacement.Fit(new(0, 0, 3000, 2000), [mainWork], mainWork) == mainWork, "Übergroßes Fenster wird auf die Arbeitsfläche ohne Taskleiste begrenzt");
store.Save(new BotOptions { Theme = "Light", AlwaysOnTop = false, WindowMaximized = true, WindowDpi = 144 });
var experience = store.Load();
Check(experience.Theme == "Light" && !experience.AlwaysOnTop && experience.WindowMaximized && experience.WindowDpi == 144, "Darstellung und Fensterzustand werden gespeichert");
var invalidTheme = new BotOptions { Theme = "invalid" }; invalidTheme.Normalize();
Check(invalidTheme.Theme == "Dark", "Unbekanntes Farbschema fällt auf Dark zurück");
var legacyTheme = new BotOptions { Theme = "System" }; legacyTheme.Normalize();
Check(legacyTheme.Theme == "Dark" && new BotOptions().Theme == "Dark", "Dark ist Standard und ersetzt den bisherigen System-Modus");
if (args.Contains("--desktop-smoke"))
{
    var monitors = Desktop.Monitors();
    Check(monitors.Length > 0, "Native Monitor-Erkennung");
    var bounds = monitors[0].Bounds;
    var frame = new BotDesktop().Capture(new(bounds.X, bounds.Y, 2, 2));
    Check(frame.Width == 2 && frame.Height == 2 && frame.Pixels.Length == 4, "Native Bildschirmaufnahme mit vollständigem Pixelpuffer");
    using var overlay = new OverlayWindow(0x0000ff);
    overlay.Show(new(bounds.X + 10, bounds.Y + 10, 40, 40)); overlay.Hide();
    Check(true, "Native Overlay-Erzeugung, Region, Anzeigen und Freigabe");
    using var window = new TestWindow();
    var desktop = new BotDesktop();
    Desktop.ShowWindow(window.Handle, 4);
    Check(Desktop.GetForegroundWindow() != window.Handle && desktop.IsAvailable(window.Handle),
        "Sichtbares inaktives Fenster ist nativ verfügbar");
    var before = desktop.GetGameBounds(window.Handle) ?? throw new Exception("Client-Bereich fehlt.");
    Desktop.SetWindowPos(window.Handle, 0, 300, 200, 1000, 700, 0x0014);
    var after = desktop.GetGameBounds(window.Handle) ?? throw new Exception("Client-Bereich fehlt nach Verschieben.");
    Check(after.X - before.X == 200 && after.Y - before.Y == 100 && after.Width - before.Width == 200 && after.Height - before.Height == 100,
        "Native Client-Koordinaten folgen Verschieben und Größenänderung");
    Desktop.ShowWindow(window.Handle, 6);
    Check(desktop.GetGameBounds(window.Handle) == null, "Minimierte Fenster liefern keinen Aufnahmebereich");
}
Console.WriteLine($"{passed} Prüfungen erfolgreich. Testdateien: {folder}");

sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
}
sealed class TestWindow : IDisposable
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint data);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyWindow(nint handle);
    public nint Handle { get; } = CreateWindowExW(0x08000080, "STATIC", "Fish geometry test", 0x00cf0000, 100, 100, 800, 600, 0, 0, 0, 0);
    public TestWindow() { if (Handle == 0) throw new Exception("Testfenster konnte nicht erstellt werden."); }
    public void Dispose() => DestroyWindow(Handle);
}
sealed class FakeDesktop : IBotDesktop
{
    public PixelRect Bounds = new(-1920, -200, 1920, 1080);
    public List<PixelRect> CapturedAreas = [];
    public int StatePixel = 0xff0000;
    public bool WindowAvailable = true, FindBobber, FindSplash;
    public List<int> Keys = [];
    public int Loots, Closes, Shutdowns, Captures;
    public PixelPoint? LastLoot;
    public PixelRect? LastExpectedBounds;
    public Action<int>? OnKey;
    public Action? OnCapture, OnLoot;
    public nint Game = 1;
    public nint FindGame() => Game;
    public bool IsAvailable(nint game) => WindowAvailable;
    public PixelRect? GetGameBounds(nint game) => WindowAvailable ? Bounds : null;
    public PixelFrame Capture(PixelRect bounds)
    {
        Captures++; CapturedAreas.Add(bounds); OnCapture?.Invoke();
        if (bounds.Width == 1) return new(1, 1, [StatePixel]);
        var pixels = new int[bounds.Width * bounds.Height];
        if (bounds.Width == 800 && FindBobber) pixels[0] = 0xff0000;
        else if (FindSplash) pixels[0] = 0xfafafa;
        return new(bounds.Width, bounds.Height, pixels);
    }
    public void Key(nint game, int key, CancellationToken cancellation) { cancellation.ThrowIfCancellationRequested(); Keys.Add(key); OnKey?.Invoke(key); }
    public Task LootAsync(nint game, PixelPoint point, PixelRect expectedBounds, CancellationToken cancellation)
    { cancellation.ThrowIfCancellationRequested(); Loots++; LastLoot = point; LastExpectedBounds = expectedBounds; OnLoot?.Invoke(); return Task.CompletedTask; }
    public void CloseGame(nint game, CancellationToken cancellation) { cancellation.ThrowIfCancellationRequested(); Closes++; }
    public void Shutdown(CancellationToken cancellation) { cancellation.ThrowIfCancellationRequested(); Shutdowns++; }
}
