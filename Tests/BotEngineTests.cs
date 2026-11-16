using Fish.Configuration;
using Fish.Core;

static class BotEngineTests
{
    public static async Task Run(Action<bool, string> check, Predicate<string>? filter = null)
    {
        async Task Exercise(string name, Action<Scenario> arrange, Action<Scenario> verify)
        {
            if (filter != null && !filter(name)) return;
            using var scenario = new Scenario();
            arrange(scenario);
            try { await scenario.Engine.RunAsync(scenario.Options, scenario.Stop.Token); }
            catch (OperationCanceledException) when (scenario.Stop.IsCancellationRequested) { }
            check(!scenario.Expired, name + " endet durch sein Prüfkriterium");
            verify(scenario);
        }

        await Exercise("Start", s => s.Desktop.OnKey = _ => s.Cancel(), s =>
            check(s.Desktop.Keys.Select(k => k.Key).SequenceEqual([0x31]) && s.Desktop.Captures.Count == 0,
                "Start fordert sofort einen Wurf an; Stop verhindert Folgeaktionen"));

        await Exercise("Konfigurierter Hotkey", s =>
        {
            s.Options.FishingHotkey = "Ctrl+Alt+F8";
            s.Desktop.OnKey = _ => s.Cancel();
        }, s => check(s.Desktop.Keys.Single() is { Key: 0x77, Modifiers: HotkeyModifiers.Control | HotkeyModifiers.Alt },
            "Auswerfen verwendet die konfigurierte Taste und ihre Modifier"));

        bool overlappingRejected = false;
        await Exercise("Überlappende Sitzung", s => s.OnUpdate = _ =>
        {
            try { s.Engine.RunAsync(s.Options, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { overlappingRejected = true; }
            s.Cancel();
        }, s => check(overlappingRejected && s.Updates.Count(u => u.State == BotState.Stopped) == 1,
            "Eine zweite Sitzung kann nicht gleichzeitig dieselbe Engine verwenden"));

        await Exercise("Nicht gesendeter Wurf", s =>
        {
            s.Desktop.CastFailures = 1; s.Desktop.OnKey = _ => s.Cancel();
        }, s => check(s.Desktop.Keys.Count == 1 && s.Updates.Count(u => u.State == BotState.Casting) == 1,
            "Ein übersprungener Eingabeversuch zählt nicht als Wurf und wird erneut versucht"));

        await Exercise("Griffbearbeitung vor Start", s =>
        {
            s.Engine.SetAreaEditing(true);
            s.OnUpdate = update =>
            {
                if (update.State == BotState.AdjustingArea) s.Engine.SetAreaEditing(false);
            };
            s.Desktop.OnKey = _ => s.Cancel();
        }, s => check(s.Desktop.Keys.Count == 1 && s.Desktop.Captures.Count == 0 &&
            s.Updates.Any(u => u.State == BotState.AdjustingArea),
            "Griffbearbeitung pausiert den Bot auch vor dem ersten Wurf und lässt sich fortsetzen"));

        await Exercise("Griffbearbeitung während Splash", s =>
        {
            bool paused = false;
            s.Desktop.OnSplashCapture = count =>
            {
                if (paused || count != 5) return;
                paused = true; s.Engine.SetAreaEditing(true);
            };
            s.OnUpdate = update => { if (update.State == BotState.AdjustingArea) s.Engine.SetAreaEditing(false); };
            s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.Desktop.Loots.Count == 1 && s.BobberFinds == 2 && s.Desktop.Keys.Count == 1,
            "Ein Splash während der Griffbearbeitung klickt nicht; nach Loslassen wird der Bobber neu gesucht"));

        await Exercise("Kurze Griffbearbeitung", s =>
        {
            bool edited = false;
            s.Desktop.OnSplashCapture = count =>
            {
                if (edited || count != 5) return;
                edited = true; s.Engine.SetAreaEditing(true); s.Engine.SetAreaEditing(false);
            };
            s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.Desktop.Loots.Count == 1 && s.BobberFinds == 2,
            "Auch eine zwischen zwei Schleifendurchläufen abgeschlossene Griffbearbeitung verwirft alte Ergebnisse"));

        await Exercise("Zeitgrenze nach Griffbearbeitung", s =>
        {
            bool paused = false;
            s.OnUpdate = update =>
            {
                if (!paused && update.State == BotState.Casting) { paused = true; s.Engine.SetAreaEditing(true); }
                else if (update.State == BotState.AdjustingArea) { s.Clock.Advance(TimeSpan.FromSeconds(30)); s.Engine.SetAreaEditing(false); }
            };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.Desktop.Keys.Count == 2 && s.Desktop.Captures.Count == 0 && s.CastTimeouts == 1,
            "Beim Ziehen keine Aufnahme oder Eingabe; abgelaufene Wurfzeit wird erst nach Loslassen behandelt"));

        var weakPreviews = new List<bool>();
        await Exercise("Zweifarben-Bestätigung", s =>
        {
            s.Desktop.BobberImage = BobberTestImages.BlueAndBody();
            s.Engine.PreviewAvailable += (_, kind, found) => { if (kind == PreviewKind.Bobber) weakPreviews.Add(found); };
            s.OnUpdate = update => { if (update.State == BotState.Fishing) s.Cancel(); };
        }, s => check(s.Desktop.Captures.Count == 3 && weakPreviews.SequenceEqual([false, false, true]) &&
            s.BobberFinds == 1 && s.Desktop.Loots.Count == 0,
            "Der Ablauf übernimmt einen Zweifarben-Bobber erst beim dritten Bild und speichert vorher kein Fundbild"));

        var hiddenBuffer = new PreviewBuffer();
        var hiddenPreviewKinds = new List<bool>();
        await Exercise("Verborgene Live-Vorschau", s =>
        {
            s.Engine.SetLivePreviewEnabled(false);
            s.Engine.PreviewAvailable += (frame, kind, found) =>
            {
                if (!found) throw new Exception("Livebild trotz verborgener Vorschau");
                hiddenPreviewKinds.Add(kind == PreviewKind.Splash);
                hiddenBuffer.Add(frame, kind, found, includeLive: false);
            };
            s.Desktop.OnLoot = s.Cancel;
        }, s =>
        {
            var images = hiddenBuffer.Take();
            check(s.Desktop.Loots.Count == 1 && hiddenPreviewKinds.SequenceEqual([false, true]) &&
                images.Live == null && images.Bobber != null && images.Splash != null,
                "Verborgene Live-Vorschau behält bestätigte Bobber- und Splash-Fundbilder und verhindert keine Bisserkennung");
        });

        var hiddenWeakPreviews = new List<bool>();
        await Exercise("Zweifarben-Bestätigung ohne Live-Vorschau", s =>
        {
            s.Engine.SetLivePreviewEnabled(false);
            s.Desktop.BobberImage = BobberTestImages.BlueAndBody();
            s.Engine.PreviewAvailable += (_, _, found) => hiddenWeakPreviews.Add(found);
            s.OnUpdate = update => { if (update.State == BotState.Fishing) s.Cancel(); };
        }, s => check(s.Desktop.Captures.Count == 3 && hiddenWeakPreviews.SequenceEqual([true]),
            "Ohne Live-Vorschau werden orange Kandidaten nicht gerendert; drei Aufnahmen bestätigen weiterhin den Bobber"));

        await Exercise("Einmaliger Zweifarben-Kandidat", s =>
        {
            s.Desktop.BobberImage = BobberTestImages.BlueAndBody();
            s.Desktop.OnBobberCapture = count => { if (count > 1) s.Desktop.FindBobber = false; };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.BobberFinds == 0 && s.Desktop.Loots.Count == 0 &&
            s.BobberTimeouts == 1,
            "Ein einzelner Zweifarben-Kandidat startet keine Splash-Suche und verlängert den Bobber-Timeout nicht"));

        await Exercise("Griffbearbeitung während Zweifarben-Bestätigung", s =>
        {
            s.Desktop.BobberImage = BobberTestImages.BlueAndBody();
            bool paused = false;
            s.Engine.PreviewAvailable += (_, kind, found) =>
            {
                if (kind == PreviewKind.Splash || found || paused) return;
                paused = true; s.Engine.SetAreaEditing(true);
            };
            s.OnUpdate = update =>
            {
                if (update.State == BotState.AdjustingArea) s.Engine.SetAreaEditing(false);
                else if (update.State == BotState.Fishing) s.Cancel();
            };
        }, s => check(s.Desktop.Captures.Count == 4 && s.BobberFinds == 1,
            "Griffbearbeitung verwirft eine angefangene Bestätigung; nach Loslassen werden drei neue Bilder verlangt"));

        await Exercise("Einstellungen während Zweifarben-Bestätigung", s =>
        {
            s.Desktop.BobberImage = BobberTestImages.BlueAndBody();
            s.Desktop.OnBobberCapture = count =>
            {
                if (count != 2) return;
                s.Options.BobberSensitivity = 55; s.Engine.UpdateOptions(s.Options);
                s.Options.BobberSensitivity = 100; // Must not mutate the published runtime snapshot.
            };
            s.OnUpdate = update => { if (update.State == BotState.Fishing) s.Cancel(); };
        }, s => check(s.Desktop.Captures.Count == 3 && s.BobberFinds == 1,
            "Live geänderte Schwimmer-Toleranz erhält die Bestätigung und wirkt bereits im aufgenommenen Bild"));

        await Exercise("Stop während Zweifarben-Bestätigung", s =>
        {
            s.Desktop.BobberImage = BobberTestImages.BlueAndBody();
            s.Engine.PreviewAvailable += (_, _, _) => s.Cancel();
        }, s => check(s.Desktop.Captures.Count == 1 && s.BobberFinds == 0 && s.Desktop.Loots.Count == 0,
            "Stop unterbricht die Bestätigung ohne Bobber-Fund und ohne Loot"));

        await Exercise("Nicht gesendeter Loot", s =>
        {
            s.Desktop.LootFailures = 1; s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.Desktop.Keys.Count == 1 && s.Desktop.Loots.Count == 1 &&
            s.Desktop.LootAttempts == 2 &&
            s.BobberFinds == 2,
            "Fehlgeschlagener Loot wird nicht gesendet; frische Bobber-Suche ermöglicht einen erneuten Versuch"));

        await Exercise("Splash-Empfindlichkeit live ändern", s =>
        {
            s.Desktop.OnSplashCapture = count =>
            {
                if (count == 9) { s.Options.SplashSensitivity = 90; s.Engine.UpdateOptions(s.Options); }
            };
            s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.Desktop.Loots.Count == 1 && s.Desktop.Keys.Count == 1 && s.BobberFinds == 1 &&
            s.Desktop.Captures.Count(c => c.Area.Width == 50) == 10,
            "Biss-Toleranz wirkt sofort ohne neue Kalibrierung und erhält Schwimmer und Wurf"));

        await Exercise("Live Toleranz Schwimmer während Bisssuche", s =>
        {
            s.Desktop.OnSplashCapture = count =>
            {
                if (count == 9) { s.Options.BobberSensitivity = 55; s.Engine.UpdateOptions(s.Options); }
            };
            s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.BobberFinds == 1 && s.Desktop.Keys.Count == 1 && s.Desktop.Loots.Count == 1 &&
            s.Desktop.Captures.Count(c => c.Area.Width == 50) == 10 &&
            s.Updates.Where(u => u.State == BotState.Fishing).Select(u => u.Progress!.StartedAt).Distinct().Count() == 1,
            "Schwimmer-Regler während Bisssuche erhält Fundpunkt, Suchschritt und Fortschrittszeit"));

        await Exercise("Live Toleranz Suchzeit", s =>
        {
            s.Desktop.FindBobber = false;
            s.Desktop.OnBobberCapture = count =>
            {
                s.Options.BobberSensitivity = count % 2 == 0 ? 50 : 80; s.Engine.UpdateOptions(s.Options);
            };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check((s.Desktop.Keys[1].At - s.Desktop.Keys[0].At).TotalSeconds is >= 7 and < 7.5 &&
            s.Updates.Where(u => u.State == BotState.SearchingBobber).Select(u => u.Progress!.StartedAt).Distinct().Count() == 1,
            "Fortlaufendes Verstellen des Schwimmer-Reglers startet die fünfsekündige Suchzeit nicht neu"));

        await Exercise("Live Toleranz Biss fortlaufend", s =>
        {
            s.Desktop.OnSplashCapture = count =>
            {
                s.Options.SplashSensitivity = count % 2 == 0 ? 70 : 80; s.Engine.UpdateOptions(s.Options);
            };
            s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.BobberFinds == 1 && s.Desktop.Keys.Count == 1 && s.Desktop.Loots.Count == 1 &&
            s.Desktop.Captures.Count(c => c.Area.Width == 50) == 10,
            "Fortlaufendes Verstellen des Biss-Reglers unterbricht weder Kalibrierung noch Erkennung"));

        await Exercise("Live Toleranz während Bissauswertung", s =>
        {
            bool changed = false;
            s.Engine.PreviewAvailable += (_, kind, found) =>
            {
                if (changed || kind != PreviewKind.Splash || !found) return;
                changed = true; s.Options.SplashSensitivity = 0; s.Engine.UpdateOptions(s.Options);
            };
            s.Desktop.OnSplashCapture = count =>
            {
                s.Desktop.EmitSplash = count != 11;
                if (count == 12) { s.Options.SplashSensitivity = 100; s.Engine.UpdateOptions(s.Options); }
            };
            s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.Desktop.Loots.Count == 1 && s.Desktop.Captures.Count(c => c.Area.Width == 50) == 12,
            "Veralteter Biss löst keinen Klick aus und blockiert danach nicht den Treffer mit neuen Werten"));

        await Exercise("Hotkey live ändern", s =>
        {
            s.OnUpdate = update =>
            {
                if (update.State != BotState.Looting) return;
                s.Options.FishingHotkey = "Shift+F6"; s.Engine.UpdateOptions(s.Options);
            };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.Desktop.Keys.Count == 2 && s.Desktop.Keys[0].Key == 0x31 &&
            s.Desktop.Keys[1] is { Key: 0x75, Modifiers: HotkeyModifiers.Shift } && s.Desktop.Loots.Count == 1,
            "Geänderter Hotkey gilt ab dem nächsten Wurf, ohne den aktuellen Wurf zu unterbrechen"));

        await Exercise("Warten auf WoW", s => { s.Desktop.Game = 0; s.OnUpdate = u => { if (u.State == BotState.WoWNotRunning) s.Cancel(); }; }, s =>
            check(s.Desktop.Keys.Count == 0 && s.Desktop.Captures.Count == 0, "Ohne WoW keine Aufnahme und keine Spieleingabe"));

        await Exercise("Minimiert", s => { s.Desktop.Available = false; s.OnUpdate = u => { if (u.State == BotState.WoWNotActive) s.Cancel(); }; }, s =>
            check(s.Desktop.Keys.Count == 0 && s.Desktop.Captures.Count == 0, "Minimiertes WoW pausiert den visuellen Ablauf"));

        await Exercise("Wieder sichtbar", s =>
        {
            s.Desktop.Available = false;
            s.Desktop.OnFind = count => { if (count >= 4) s.Desktop.Available = true; };
            s.Desktop.OnKey = _ => s.Cancel();
        }, s => check(s.Desktop.Keys.Count == 1 && s.Desktop.Available, "Nach Wiederherstellung wird automatisch ausgeworfen"));

        await Exercise("Bobber-Timeout", s =>
        {
            s.Desktop.FindBobber = false;
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s =>
        {
            double elapsed = (s.Desktop.Keys[1].At - s.Desktop.Keys[0].At).TotalSeconds;
            check(elapsed >= 7 && elapsed < 7.5 && s.BobberTimeouts == 1 &&
                s.Desktop.Keys.All(k => k.Key == 0x31), "Ohne Bobber nach 2 s Anwurf und vollen 5 s Suche erneut auswerfen");
            var casting = s.Updates.First(u => u.State == BotState.Casting).Progress!;
            var start = s.Updates.First(u => u.State == BotState.SearchingBobber).Progress!;
            var timeout = s.Updates.Select(u => u.CompletedStep).OfType<StepCompletion>().First();
            check(casting.State == BotState.Casting && casting.StartedAt == s.Desktop.Keys[0].At && casting.MaximumSeconds == 2 &&
                start.State == BotState.SearchingBobber && start.StartedAt >= casting.Deadline &&
                start.MaximumSeconds == 5 && timeout is { Success: false, Progress.State: BotState.SearchingBobber } &&
                s.Updates.Any(u => u.State == BotState.Casting && u.CompletedStep == timeout),
                "Anwurf-Fortschritt hat 2 s; Bobber-Fortschritt startet danach bei 0 von 5 s und zeigt Timeout rot");
            check(s.Desktop.Captures.All(c => c.At >= casting.Deadline), "Während der zweisekündigen Anwurfphase keine Suchaufnahmen");
            check(start.ElapsedSeconds(start.StartedAt.AddSeconds(-1)) == 0 &&
                start.ElapsedSeconds(start.Deadline.AddSeconds(1)) == 5,
                "Fortschrittswerte bleiben zwischen null und dem Zeitlimit");
        });

        await Exercise("Bobber spät nach Anwurf", s =>
        {
            s.Desktop.OnBobberCapture = count => { if (count == 1) s.Clock.Advance(TimeSpan.FromSeconds(4)); };
            s.OnUpdate = update => { if (update.State == BotState.Fishing) s.Cancel(); };
        }, s => check(s.BobberFinds == 1 && s.Desktop.Keys.Count == 1 && s.Desktop.Loots.Count == 0 &&
            s.Updates.First(u => u.State == BotState.Fishing).CompletedStep!.CompletedAt >= s.Desktop.Keys[0].At.AddSeconds(6),
            "Bobber-Treffer nach insgesamt sechs Sekunden ist innerhalb der fünfsekündigen Suchphase gültig"));

        await Exercise("Angel-Timeout", s =>
        {
            s.Desktop.EmitSplash = false;
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s =>
        {
            double elapsed = (s.Desktop.Keys[1].At - s.Desktop.Keys[0].At).TotalSeconds;
            check(elapsed >= 25 && elapsed < 25.5 && s.Desktop.Loots.Count == 0 && s.CastTimeouts == 1,
                "Ohne Splash spätestens nach 25 Sekunden ab Wurf erneut auswerfen");
            var splash = s.Updates.First(u => u.State == BotState.Fishing).Progress!;
            check(splash is { State: BotState.Fishing, MaximumSeconds: > 0 and < 25 } &&
                splash.Deadline == s.Desktop.Keys[0].At.AddSeconds(25) &&
                s.Updates.Any(u => u.CompletedStep is { Success: false, Progress.State: BotState.Fishing }),
                "Splash-Fortschritt startet beim Bobber-Fund und endet rot am gesamten Wurfzeitlimit");
        });

        await Exercise("Loot-Zyklus", s => s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); }, s =>
        {
            check(s.Desktop.Loots.Count == 1, "Visueller Zyklus fordert Loot an" + (s.Desktop.Loots.Count == 1 ? "" : ": " +
                string.Join(" | ", s.Updates.Select(u => u.State + ": " + u.Activity))));
            var click = s.Desktop.Loots.Single();
            var area = s.Desktop.Captures.First(c => c.Area.Width == 160).Area;
            var expected = new PixelPoint(area.X + 23 + ReferenceBobber.Default.Anchor.X, area.Y + 31 + ReferenceBobber.Default.Anchor.Y);
            check(Math.Abs(click.Point.X - expected.X) <= 2 && Math.Abs(click.Point.Y - expected.Y) <= 2,
                "Splash-Bewegung löst den Klick am Bobber mit negativen Bildschirmkoordinaten aus");
            check((s.Desktop.Keys[1].At - click.At).TotalMilliseconds >= FishingDefaults.LootDelayMilliseconds &&
                s.Desktop.Captures.All(c => c.At <= click.At), "Nach dem Klick kurze Loot-Wartezeit ohne weitere Splash-Aufnahmen, dann erneut auswerfen");
            var results = s.Updates.Select(u => u.CompletedStep).OfType<StepCompletion>().DistinctBy(c => c.Id).ToArray();
            check(results is [{ Id: 1, Success: true, Progress.State: BotState.SearchingBobber },
                { Id: 2, Success: true, Progress.State: BotState.Fishing }] &&
                results[1].Progress.StartedAt == results[0].CompletedAt,
                "Bobber- und Splash-Treffer liefern je einen grünen Abschluss; Splash-Zeit startet bei null");
            var wait = s.Updates.First(u => u.State == BotState.Looting).Progress!;
            check(wait.State == BotState.Looting && wait.MaximumSeconds == FishingDefaults.LootDelayMilliseconds / 1000.0 &&
                s.Updates.Last() is { State: BotState.Stopped, Progress: null, CompletedStep: null },
                "Loot-Wartezeit hat eigenen Fortschritt; Stop verwirft Zeit und Trefferanzeige");
        });

        await Exercise("Zwei Würfe", s => s.Desktop.OnLoot = () => { if (s.Desktop.Loots.Count == 2) s.Cancel(); }, s =>
            check(s.Desktop.Keys.Count == 2 && s.Desktop.Loots.Count == 2 &&
                s.BobberFinds == 2,
                "Jeder neue Wurf sucht den Bobber und erhält eine frische Splash-Referenz"));

        await Exercise("Stop im Splash", s => s.Desktop.OnSplashCapture = count => { if (count == 5) s.Cancel(); }, s =>
            check(s.Desktop.Loots.Count == 0 && s.Desktop.Keys.Count == 1, "Stop während der Splash-Aufnahme verhindert Loot und erneutes Auswerfen"));

        await Exercise("Stop nach Klick", s => s.OnUpdate = update => { if (update.State == BotState.Looting) s.Cancel(); }, s =>
            check(s.Desktop.Loots.Count == 1 && s.Desktop.Keys.Count == 1, "Stop während der Loot-Wartezeit verhindert den nächsten Wurf"));

        await Exercise("Fensterbewegung", s =>
        {
            bool moved = false;
            s.Desktop.OnCapture = area => { if (!moved && area.Width == 160) { moved = true; s.Desktop.Bounds = s.Desktop.Bounds with { X = 100, Y = 50 }; } };
            s.Desktop.OnLoot = s.Cancel;
        }, s => check(s.Desktop.Loots.Count == 1 && s.Desktop.Loots[0].Bounds == s.Desktop.Bounds &&
            s.BobberFinds == 1, "Fensterbewegung während Aufnahme verwirft veraltete Fundpunkte"));

        await Exercise("Bewegung nach Klick", s =>
        {
            s.Desktop.OnLoot = () => s.Desktop.Bounds = s.Desktop.Bounds with { X = 100 };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.Desktop.Loots.Count == 1 && s.Desktop.Keys.Count == 2, "Fensterbewegung nach dem Klick löst keinen zweiten Loot-Versuch im alten Wurf aus"));

        await Exercise("Neues Fenster", s =>
        {
            s.Desktop.OnCapture = area => { if (area.Width == 160) s.Desktop.Game = 2; };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.Desktop.Keys.Select(k => k.Game).SequenceEqual([(nint)1, (nint)2]) && s.Desktop.Loots.Count == 0,
            "Neues WoW-Fenster erhält einen eigenen Wurf und keine alte Loot-Position"));

        await Exercise("Später Bobber-Treffer", s =>
        {
            s.Desktop.OnCapture = area => { if (area.Width == 160) s.Clock.Advance(TimeSpan.FromSeconds(6)); };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.BobberFinds == 0 && s.Desktop.Loots.Count == 0,
            "Ein nach Ablauf des Such-Timeouts berechneter Treffer wird verworfen"));

        await Exercise("Bildwechsel", s =>
        {
            s.Desktop.SceneChanges = true;
            // Include simulated matcher work so repeated reacquisition reaches the overall deadline quickly in wall time.
            s.Desktop.OnCapture = area => { if (area.Width == 160) s.Clock.Advance(TimeSpan.FromSeconds(1)); };
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.Desktop.Loots.Count == 0 && s.Updates.Any(u => u.Activity.StartsWith("Bildwechsel")) &&
            (s.Desktop.Keys[1].At - s.Desktop.Keys[0].At).TotalSeconds >= 25,
            "Wiederholte Referenzwechsel verlängern die gesamte Angelzeit nicht unbegrenzt"));

        await Exercise("Live-Schwelle", s =>
        {
            s.Options.BobberSensitivity = 0;
            bool posted = false;
            s.Desktop.OnCapture = area =>
            {
                if (posted || area.Width != 160) return;
                posted = true; s.Options.BobberSensitivity = 20;
                s.Engine.UpdateOptions(s.Options);
                s.Options.BobberSensitivity = 0; // Must not mutate the already published snapshot.
            };
            s.Desktop.OnLoot = s.Cancel;
            s.Desktop.OnKey = _ => { if (s.Desktop.Keys.Count == 2) s.Cancel(); };
        }, s => check(s.Desktop.Loots.Count == 1 && s.Desktop.Keys.Count == 1 && s.Desktop.Captures.Count(c => c.Area.Width == 160) >= 2,
            "Neue Bobber-Schwelle wirkt im laufenden Wurf und ist vom veränderlichen UI-Modell entkoppelt"));

        await Exercise("Langes Warten auf WoW", s =>
        {
            s.Desktop.Game = 0;
            s.Desktop.OnFind = count =>
            {
                if (count == 1) s.Clock.Advance(TimeSpan.FromSeconds(300));
                else s.Cancel();
            };
        }, s => check(s.Desktop.Keys.Count == 0 && s.Desktop.Loots.Count == 0 && s.Desktop.Captures.Count == 0,
            "Auch nach fünf Minuten ohne WoW keine automatischen Folgeaktionen"));
    }

    private sealed class Scenario : IDisposable
    {
        public readonly SimulatedTime Clock = new();
        public readonly CancellationTokenSource Stop = new();
        private readonly Timer _watchdog;
        public bool Expired;
        public readonly SimulatedDesktop Desktop;
        public readonly BotEngine Engine;
        // Fixed geometry; the synthetic splash starts after the fixed one-second calibration.
        public readonly BotOptions Options = new() { BobberAreaWidth = 160, BobberAreaHeight = 140,
            BobberSensitivity = 20, BobberAreaYOffset = 30, SplashSensitivity = 80 };
        private IEnumerable<StepCompletion> Completions => Updates.Select(u => u.CompletedStep).OfType<StepCompletion>().DistinctBy(c => c.Id);
        public int BobberFinds => Completions.Count(c => c.Success && c.Progress.State == BotState.SearchingBobber);
        public int BobberTimeouts => Completions.Count(c => !c.Success && c.Progress.State == BotState.SearchingBobber && c.CompletedAt < c.Progress.StartedAt.AddSeconds(25));
        public int CastTimeouts => Completions.Count(c => !c.Success && (c.Progress.State == BotState.Fishing || c.CompletedAt >= c.Progress.StartedAt.AddSeconds(25)));
        public readonly List<BotUpdate> Updates = [];
        public Action<BotUpdate>? OnUpdate;
        public Scenario()
        {
            Desktop = new(Clock); Engine = new(Desktop, Clock);
            Engine.Updated += update => { Updates.Add(update); OnUpdate?.Invoke(update); };
            _watchdog = new(_ => { Expired = true; Stop.Cancel(); }, null, TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan);
        }
        public void Cancel() => Stop.Cancel();
        public void Dispose() { _watchdog.Dispose(); Stop.Dispose(); }
    }

    private sealed class SimulatedDesktop(SimulatedTime clock) : IBotDesktop
    {
        public nint Game = 1;
        public bool Available = true, FindBobber = true, EmitSplash = true, SceneChanges;
        public PixelRect Bounds = new(-1920, -200, 1920, 1080);
        public readonly List<(DateTimeOffset At, nint Game, int Key, HotkeyModifiers Modifiers)> Keys = [];
        public readonly List<(DateTimeOffset At, PixelPoint Point, PixelRect Bounds)> Loots = [];
        public readonly List<(DateTimeOffset At, PixelRect Area)> Captures = [];
        private int _findCalls, _splashCaptures;
        private PixelRect? _lastSplashArea;
        public Action<int>? OnFind, OnKey, OnSplashCapture;
        public Action<int>? OnBobberCapture;
        public PixelFrame? BobberImage;
        private int _bobberCaptures;
        public Action<PixelRect>? OnCapture;
        public Action? OnLoot;
        public int CastFailures, LootFailures, LootAttempts;
        public nint FindGame() { OnFind?.Invoke(++_findCalls); return Game; }
        public bool IsAvailable(nint game) => Available && game == Game && game != 0;
        public PixelRect? GetGameBounds(nint game) => IsAvailable(game) ? Bounds : null;
        public PixelFrame Capture(PixelRect area)
        {
            if (area.Width == 1 && area.Height == 1) throw new Exception("Unerwartete 1×1-Aufnahme statt Suchbereich.");
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Captures.Add((clock.GetUtcNow(), area)); OnCapture?.Invoke(area);
            var frame = new PixelFrame(area.Width, area.Height, new int[area.Width * area.Height]);
            if (area.Width == 160)
            {
                _splashCaptures = 0; _lastSplashArea = null;
                OnBobberCapture?.Invoke(++_bobberCaptures);
                if (FindBobber) BobberTestImages.Paste(frame, BobberImage ?? ReferenceBobber.Default.Image, 23, 31);
            }
            else
            {
                if (_lastSplashArea != area) { _lastSplashArea = area; _splashCaptures = 0; }
                _splashCaptures++;
                OnSplashCapture?.Invoke(_splashCaptures);
                if (_splashCaptures >= 10)
                {
                    if (SceneChanges) Array.Fill(frame.Pixels, 0xffffff);
                    else if (EmitSplash)
                        for (int y = 10; y < Math.Min(40, area.Height); y++)
                        for (int x = 10; x < Math.Min(50, area.Width); x++) frame.Pixels[y * area.Width + x] = 0x405060;
                }
            }
            return frame;
        }
        public Task<bool> CastAsync(nint game, FishingHotkey hotkey, PixelRect expectedBounds, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (CastFailures > 0) { CastFailures--; return Task.FromResult(false); }
            Keys.Add((clock.GetUtcNow(), game, hotkey.Key, hotkey.Modifiers)); OnKey?.Invoke(hotkey.Key);
            return Task.FromResult(true);
        }
        public Task<bool> LootAsync(nint game, PixelPoint point, PixelRect bounds, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            LootAttempts++;
            if (LootFailures > 0) { LootFailures--; return Task.FromResult(false); }
            Loots.Add((clock.GetUtcNow(), point, bounds)); OnLoot?.Invoke(); return Task.FromResult(true);
        }
    }

    private sealed class SimulatedTime : TimeProvider
    {
        private readonly object _gate = new();
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() { lock (_gate) return _now; }
        public void Advance(TimeSpan span) { lock (_gate) _now += span; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => new AdvancingTimer(this, callback, state, dueTime);

        private sealed class AdvancingTimer : ITimer
        {
            private readonly SimulatedTime _clock;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private readonly Timer _timer;
            private TimeSpan _due;
            private volatile bool _disposed;
            public AdvancingTimer(SimulatedTime clock, TimerCallback callback, object? state, TimeSpan due)
            {
                _clock = clock; _callback = callback; _state = state;
                _timer = new(_ => { if (!_disposed) { _clock.Advance(_due); _callback(_state); } }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                Change(due, Timeout.InfiniteTimeSpan);
            }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _due = dueTime;
                return _timer.Change(dueTime == Timeout.InfiniteTimeSpan ? dueTime : TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan);
            }
            public void Dispose() { _disposed = true; _timer.Dispose(); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
