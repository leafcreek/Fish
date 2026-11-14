using Fish.Configuration;
using System.Diagnostics;

namespace Fish.Core;

public sealed record BotUpdate(BotState State, string Activity, PixelRect? BobberArea, PixelRect? SplashArea,
    int BobberMatchMinimum, int SplashChangePercent);

/// <summary>One serial session. The owner cancels and awaits it before starting another.</summary>
public sealed class BotEngine(IBotDesktop desktop, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly BobberTemplateMatcher _bobberMatcher = new(BobberTemplate.Default);
    public event Action<BotUpdate>? Updated;
    public event Action<BotLogEntry>? Logged;
    public event Action<DateTimeOffset, double>? ScanMeasured;
    public event Action<PixelFrame, bool, bool, SplashMeasurement?>? PreviewAvailable;

    public async Task RunAsync(BotOptions source, CancellationToken cancellation)
    {
        var options = source.Copy();
        options.Normalize();
        var motion = new SplashMotionDetector(options.SplashPixelTolerance, options.SplashChangePercent,
            options.SplashAdaptiveThreshold, options.SplashCalibrationMilliseconds);
        SplashMeasurement? motionStatus = null;
        var now = _time.GetUtcNow();
        var startedAt = now;
        int casts = 0, lootRequests = 0, lootSignals = 0, timeouts = 0;
        string endReason = "Sitzung abgeschlossen";
        DateTimeOffset? lastGeometryLog = null, lastMotionResetLog = null;
        void Log(string id, string category, string message, LogLevel level = LogLevel.Info)
            => Logged?.Invoke(new(_time.GetUtcNow(), level, category, id, message));
        string DetectionSettings() => $"Bobber-Mindestübereinstimmung {options.BobberMatchMinimum} %, Splash ab {options.SplashChangePercent} %, Pixel-Rauschtoleranz {options.SplashPixelTolerance}";
        var nextAction = now;
        var lastActivity = now;

        DateTimeOffset? castTime = null, closeAt = null, shutdownAt = null;
        nint game = 0, countdownGame = 0;
        bool looted = false, timedOut = false, lootRequested = false;
        bool splashDetected = false;
        BotState state = BotState.None;
        PixelRect? splashArea = null;
        PixelPoint? bobberPoint = null;
        string countdownReason = "";
        PixelRect? gameBounds = null;
        PixelRect bobberArea = default;

        string FishingActivity() => lootRequested ? "Warte auf Loot-Bestätigung" : splashArea == null ? "Bobber suchen" :
            motionStatus?.Ready == true ? "Bewegung vergleichen" : "Splash kalibrieren";
        void ResetObservation()
        {
            splashArea = null; bobberPoint = null; motionStatus = null; motion.Reset();
        }
        BotUpdate? previousUpdate = null;
        void Publish(string activity)
        {
            var update = new BotUpdate(state, activity,
                gameBounds != null ? bobberArea : null, gameBounds != null && state == BotState.Fishing ? splashArea : null,
                options.BobberMatchMinimum, options.SplashChangePercent);
            if (update == previousUpdate) return;
            previousUpdate = update;
            Updated?.Invoke(update);
        }
        void Schedule(bool close, bool shutdown, string reason)
        {
            countdownReason = reason;
            countdownGame = game;
            closeAt = close ? now.AddSeconds(60) : null;
            shutdownAt = shutdown ? now.AddSeconds(120) : null;
            ResetObservation();
            Log("action.scheduled", "Countdown", $"{reason}. " + (close ? "WoW schließen in 60 s. " : "") + (shutdown ? "Windows herunterfahren in 120 s. " : "") + "Stop bricht beide Aktionen ab.", LogLevel.Warning);
        }
        string Triggers(bool inactive, bool full, bool dead)
        {
            var triggers = new List<string>();
            if (inactive) triggers.Add("120 s ohne Loot");
            if (full) triggers.Add("Inventar voll");
            if (dead) triggers.Add("Tod");
            return triggers.Count == 0 ? "aus" : string.Join(", ", triggers);
        }
        bool CanAct() => !cancellation.IsCancellationRequested && desktop.FindGame() == game && desktop.IsAvailable(game) && gameBounds != null && desktop.GetGameBounds(game) == gameBounds;
        Log("session.started", "Sitzung", $"Angeln gestartet · Taste 1 · Scan alle {options.PerformanceScanRate} ms · Wurfdauer maximal {options.FeaturesCastTime} s.");
        Log("session.detection", "Erkennung", $"Bobber: Template Matching · Forever-Bildvorlage · Größe 60–140 %, Drehung ±30°, Breite 80–120 %, Perspektive ±18 % · {DetectionSettings()} · Splash-Bildvergleich alle {options.PerformanceScanRate} ms · Kalibrierung {options.SplashCalibrationMilliseconds} ms · Wasserbewegung {(options.SplashAdaptiveThreshold ? "berücksichtigen" : "ignorieren")}.");
        Log("session.actions", "Aktionen", $"WoW schließen nach 60 s: {Triggers(options.FeaturesExitOnInactivity, options.FeaturesExitOnFullBag, options.FeaturesExitOnDeath)} · Windows herunterfahren nach 120 s: {Triggers(options.FeaturesShutdownOnInactivity, options.FeaturesShutdownOnFullBag, options.FeaturesShutdownOnDeath)}.");
        try
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                now = _time.GetUtcNow();
                if (closeAt != null || shutdownAt != null)
                {
                    if (closeAt is { } close && now >= close)
                    {
                        desktop.CloseGame(countdownGame, cancellation);
                        closeAt = null;
                        Log("action.close", "Aktion", "Countdown abgelaufen. Schließen von WoW angefordert.", LogLevel.Warning);
                    }
                    if (shutdownAt is { } shutdown && now >= shutdown)
                    {
                        Log("action.shutdown", "Aktion", "Countdown abgelaufen. Windows wird zum Herunterfahren aufgefordert.", LogLevel.Warning);
                        desktop.Shutdown(cancellation);
                        shutdownAt = null;
                    }
                    if (closeAt == null && shutdownAt == null) return;
                    var deadline = closeAt ?? shutdownAt!.Value;
                    Publish($"{countdownReason} · {(closeAt != null ? "WoW schließen" : "Windows herunterfahren")} in {Math.Max(0, Math.Ceiling((deadline - now).TotalSeconds))} s · Stop zum Abbrechen");
                    await Task.Delay(100, cancellation).ConfigureAwait(false);
                    continue;
                }
                nint foundGame = desktop.FindGame();
                if (foundGame != game)
                {
                    castTime = null;
                    ResetObservation(); gameBounds = null;
                    looted = timedOut = lootRequested = false;
                    splashDetected = false;
                    nextAction = now;
                    state = BotState.None;
                    game = foundGame;
                    Log("window.changed", "Fenster", game == 0 ? "WoW-Fenster verloren. Warte auf WoW." : "WoW-Fenster verbunden. Vorherige Fundpunkte und Wurfzustände verworfen.");
                }
                var currentBounds = desktop.GetGameBounds(game);
                if (currentBounds != gameBounds)
                {
                    if (currentBounds is { } changedBounds && (lastGeometryLog == null || (now - lastGeometryLog.Value).TotalSeconds >= 3))
                    {
                        Log("window.geometry", "Erkennung", $"Spielfläche {changedBounds.Width} × {changedBounds.Height} bei ({changedBounds.X}, {changedBounds.Y}). Suchbereiche neu berechnet.");
                        lastGeometryLog = now;
                    }
                    // A changed layout invalidates the previous detection; find the bobber again.
                    ResetObservation();
                    gameBounds = currentBounds;
                    if (gameBounds is { } client)
                        bobberArea = Detection.Around(new(client.X + client.Width / 2, client.Y + client.Height / 2),
                            options.BobberAreaWidth, options.BobberAreaHeight, options.BobberAreaXOffset, options.BobberAreaYOffset, client);
                }
                BotState next = game == 0 ? BotState.WoWNotRunning : gameBounds == null || !desktop.IsAvailable(game)
                    ? BotState.WoWNotActive : Detection.State(desktop.Capture(new(gameBounds!.Value.X, gameBounds.Value.Y, 1, 1)).Pixels[0]);
                if (gameBounds != null && desktop.GetGameBounds(game) != gameBounds)
                {
                    await Task.Delay(options.PerformanceScanRate, cancellation).ConfigureAwait(false);
                    continue;
                }
                if (next != state)
                {
                    if (next == BotState.Idle && state == BotState.Fishing && splashDetected && !timedOut && !looted)
                        Log("cast.no-fish", "Angeln", "Kein Fisch angebissen · Wurf nach erkanntem Splash ohne Loot-Bestätigung beendet.", LogLevel.Warning);
                    if (next != BotState.Fishing) ResetObservation();
                    state = next;
                    Log("state.changed", "Status", state switch
                    {
                        BotState.WoWNotRunning => "WoW nicht gefunden. Warte auf das Spielfenster.",
                        BotState.WoWNotActive => "WoW nicht sichtbar oder minimiert. Aufnahme und Spieleingaben pausieren.",
                        BotState.WANotFound => "WeakAura nicht erkannt. Statuspixel oben links im Spielinhalt prüfen; keine Spieleingaben.",
                        _ => StateText(state)
                    }, state is BotState.WANotFound or BotState.Dead or BotState.FullBag ? LogLevel.Warning : LogLevel.Info);
                    if (state == BotState.Looting) { looted = true; lastActivity = now; lootSignals++; Log("loot.confirmed", "Loot", $"WeakAura meldet Loot · Bestätigung {lootSignals}.", LogLevel.Success); }
                    if (state is BotState.Dead or BotState.FullBag)
                    {
                        bool dead = state == BotState.Dead;
                        bool close = dead ? options.FeaturesExitOnDeath : options.FeaturesExitOnFullBag;
                        bool shutdown = dead ? options.FeaturesShutdownOnDeath : options.FeaturesShutdownOnFullBag;
                        if (!close && !shutdown) { endReason = $"{StateText(state)}; keine Folgeaktion aktiviert"; Publish(StateText(state)); return; }
                        Schedule(close, shutdown, StateText(state));
                        continue;
                    }
                }
                if ((now - lastActivity).TotalSeconds >= 120)
                {
                    lastActivity = now;
                    Log("session.inactive", "Sitzung", "Seit 120 Sekunden kein neuer Loot-Status erkannt.", LogLevel.Warning);
                    if (options.FeaturesExitOnInactivity || options.FeaturesShutdownOnInactivity)
                    {
                        Schedule(options.FeaturesExitOnInactivity, options.FeaturesShutdownOnInactivity, "Inaktivität");
                        continue;
                    }
                }
                Publish(state == BotState.Fishing && !timedOut ? FishingActivity() : StateText(state));
                if (now >= nextAction && CanAct())
                {
                    if (state == BotState.Idle)
                    {

                        cancellation.ThrowIfCancellationRequested();
                        desktop.Key(game, 0x31, cancellation);
                        castTime = now;
                        ResetObservation();
                        looted = timedOut = lootRequested = false;
                        splashDetected = false;
                        nextAction = now.AddSeconds(2);
                        casts++; Log("cast.requested", "Angeln", $"Wurf {casts} angefordert · Taste 1 gesendet · 2 s Wartezeit.");
                    }
                    else if (state == BotState.Fishing)
                    {
                        castTime ??= now; // Also supports attaching to an already running cast.
                        if ((now - castTime.Value).TotalSeconds >= options.FeaturesCastTime)
                        {
                            if (!timedOut)
                            {
                                desktop.Key(game, 0x1b, cancellation);
                                timedOut = true;
                                timeouts++; Log("cast.timeout", "Angeln", $"Maximale Wurfdauer von {options.FeaturesCastTime} s erreicht. Abbruch per Escape angefordert · Timeout {timeouts}.", LogLevel.Warning);
                            }
                        }
                        else if (!timedOut && bobberArea.Width > 0 && bobberArea.Height > 0)
                        {
                            bool splash = splashArea != null;
                            var bounds = splashArea ?? bobberArea;
                            long scanStarted = Stopwatch.GetTimestamp();
                            var frame = desktop.Capture(bounds);
                            if (!CanAct())
                            {
                                await Task.Delay(options.PerformanceScanRate, cancellation).ConfigureAwait(false);
                                continue;
                            }
                            var bobberMatch = splash ? null : _bobberMatcher.Find(frame, options.BobberMatchMinimum, cancellation);
                            var analysis = splash ? motion.Analyze(frame, _time.GetUtcNow()) : null;
                            if (!CanAct())
                            {
                                motion.Reset(); motionStatus = null;
                                await Task.Delay(options.PerformanceScanRate, cancellation).ConfigureAwait(false);
                                continue;
                            }
                            ScanMeasured?.Invoke(_time.GetUtcNow(), Stopwatch.GetElapsedTime(scanStarted).TotalMilliseconds);
                            var preview = analysis?.Preview ?? (bobberMatch == null ? frame : BobberTemplateMatcher.Mark(frame, bobberMatch));
                            bool found = splash ? analysis!.Measurement.NewTrigger && !lootRequested : bobberMatch != null;
                            PreviewAvailable?.Invoke(preview, splash, found, analysis?.Measurement);

                            if (bobberMatch != null)
                            {
                                bobberPoint = new(bounds.X + bobberMatch.Point.X, bounds.Y + bobberMatch.Point.Y);
                                splashArea = Detection.Around(bobberPoint.Value, options.SplashAreaWidth,
                                    options.SplashAreaHeight, options.SplashAreaXOffset, options.SplashAreaYOffset, gameBounds!.Value);
                                if (splashArea.Value.Width == 0 || splashArea.Value.Height == 0)
                                    throw new InvalidOperationException("Der Splash-Suchbereich liegt außerhalb des WoW-Fensters.");
                                motion.Reset(); motionStatus = null;
                                nextAction = _time.GetUtcNow();
                                Publish(FishingActivity());
                                Log("bobber.found", "Erkennung", $"Bobber bei ({bobberPoint.Value.X}, {bobberPoint.Value.Y}) gefunden · Template {bobberMatch.Confidence:P1} · Größe {bobberMatch.Scale:P0} · Drehung {bobberMatch.RotationDegrees:F1}° · Breite {bobberMatch.AspectRatio:P0} · Perspektive {bobberMatch.Perspective:P1} · Splash-Vergleich {splashArea.Value.Width} × {splashArea.Value.Height} px.", LogLevel.Success);
                            }
                            else if (analysis != null)
                            {
                                motionStatus = analysis.Measurement;
                                if (motionStatus.SceneChanged && !lootRequested)
                                {
                                    ResetObservation();
                                    if (lastMotionResetLog == null || (_time.GetUtcNow() - lastMotionResetLog.Value).TotalSeconds >= 3)
                                    {
                                        lastMotionResetLog = _time.GetUtcNow();
                                        Log("splash.scene-changed", "Erkennung", "Großflächiger Bildwechsel. Referenz verworfen; Bobber neu suchen und Wasserbewegung kalibrieren.");
                                    }
                                }
                                else if (motionStatus.NewTrigger && !lootRequested && bobberPoint is { } point && CanAct())
                                {
                                    splashDetected = true; lootRequested = true;
                                    await desktop.LootAsync(game, point, gameBounds!.Value, cancellation).ConfigureAwait(false);
                                    lootRequests++;
                                    Log("loot.requested", "Loot", $"Splash: {motionStatus.ChangedPercent:F1} % Änderung · Grenze {motionStatus.ThresholdPercent:F1} % · zusammenhängend {motionStatus.LargestComponentPercent:F1} % · Loot-Anfrage {lootRequests} am Bobber ({point.X}, {point.Y}) · Bestätigung steht aus.");
                                }
                                Publish(FishingActivity());
                            }
                        }
                    }
                }
                await Task.Delay(options.PerformanceScanRate, cancellation).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            endReason = "Abbruch angefordert";
            throw;
        }
        catch (Exception)
        {
            endReason = "Fehler; Sitzung abgebrochen";
            throw;
        }
        finally
        {
            Updated?.Invoke(new(state, "Gestoppt", null, null, options.BobberMatchMinimum, options.SplashChangePercent));
            if (closeAt != null || shutdownAt != null)
                Log("action.cancelled", "Countdown", "Ausstehende Folgeaktionen verworfen; kein weiterer Countdown aktiv.");
            var duration = _time.GetUtcNow() - startedAt;
            Log("session.ended", "Sitzung", $"{endReason} · Dauer {duration.TotalMinutes:F1} min · {casts} Würfe angefordert · {lootRequests} Loot-Anfragen · {lootSignals} Loot-Bestätigungen · {timeouts} Timeouts. {DetectionSettings()}.");
        }
    }

    public static string StateText(BotState state) => state switch
    {
        BotState.WoWNotRunning => "WoW nicht gefunden", BotState.WoWNotActive => "WoW nicht sichtbar oder minimiert",
        BotState.WANotFound => "WeakAura nicht erkannt", BotState.Idle => "Bereit zum Auswerfen",
        BotState.Fishing => "Angeln", BotState.Looting => "Loot",
        BotState.Dead => "Charakter tot", BotState.FullBag => "Inventar voll", _ => "Bereit"
    };
}
