using Fish.Configuration;

namespace Fish.Core;

/// <summary>Serial visual cycle. Detection, input and live changes never run concurrently.</summary>
internal sealed class FishingSession(BotEngine owner, IBotDesktop desktop, TimeProvider time, FishingOptions options)
{
    private FishingOptions _options = options;
    private SplashMotionDetector _motion = options.Motion.CreateDetector();
    private bool _motionReady;
    private readonly BobberConfirmation _confirmation = new();
    private BotState _phase = BotState.Idle;
    private BotUpdate? _published;
    private nint _game;
    private PixelRect? _bounds, _splashArea;
    private PixelRect _bobberArea;
    private PixelPoint? _bobber;
    private DateTimeOffset? _castAt, _searchAt, _splashAt;
    private DateTimeOffset _bobberReadyAt, _lootAt;
    private int _areaEditVersion;
    private StepCompletion? _completedStep;
    private int _completionId;
    private DateTimeOffset Now => time.GetUtcNow();

    private void Publish(BotState state, string activity, StepProgress? progress = null)
    {
        var update = new BotUpdate(state, activity, _bounds != null ? _bobberArea : null,
            _phase == BotState.Fishing ? _splashArea : null, _game, _phase == BotState.Fishing ? _bobber : null,
            progress ?? CurrentProgress(), _completedStep);
        if (update == _published) return;
        _published = update; owner.Publish(update);
    }

    private StepProgress? CurrentProgress()
    {
        if (_phase == BotState.Looting)
            return new(_phase, _lootAt, _lootAt.AddMilliseconds(_options.LootDelayMilliseconds));
        if (_castAt is not { } castAt) return null;
        var castDeadline = castAt.AddSeconds(_options.CastTimeoutSeconds);
        if (_phase == BotState.Casting)
            return new(_phase, castAt, _bobberReadyAt < castDeadline ? _bobberReadyAt : castDeadline);
        if (_phase == BotState.SearchingBobber && _searchAt is { } searchAt)
        {
            var deadline = searchAt.AddSeconds(_options.BobberTimeoutSeconds);
            return new(BotState.SearchingBobber, searchAt, deadline < castDeadline ? deadline : castDeadline);
        }
        return _phase == BotState.Fishing && _splashAt is { } splashAt
            ? new(BotState.Fishing, splashAt, castDeadline) : null;
    }

    private void CompleteStep(bool success)
    {
        if (CurrentProgress() is { } progress)
            _completedStep = new(++_completionId, progress, Now, success);
    }

    public async Task RunAsync(CancellationToken cancellation)
    {
        try
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                ApplyOptions();
                await TickAsync(cancellation).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(_options.ScanIntervalMilliseconds), time, cancellation).ConfigureAwait(false);
            }
        }
        finally
        {
            owner.Publish(new(BotState.Stopped, "Gestoppt"));
        }
    }

    private bool UpdateWindow()
    {
        nint game = desktop.FindGame();
        if (game != _game)
        {
            _game = game; _bounds = null; _phase = BotState.Idle; _castAt = _searchAt = null; ResetObservation();
        }
        var bounds = desktop.GetGameBounds(_game);
        if (bounds != _bounds)
        {
            _bounds = bounds; ResetObservation(reacquire: true);
            if (bounds is { } client)
            {
                _bobberArea = _options.BobberRegion.In(client);
            }
        }
        if (_game != 0 && _bounds != null && desktop.IsAvailable(_game)) return true;
        ResetObservation(reacquire: true);
        Publish(_game == 0 ? BotState.WoWNotRunning : BotState.WoWNotActive,
            _game == 0 ? "Warte auf das WoW-Fenster" : "WoW nicht sichtbar oder minimiert · pausiert");
        return false;
    }

    private async Task TickAsync(CancellationToken cancellation)
    {
        ApplyAreaEdits();
        if (owner.IsAreaEditing)
        {
            Publish(BotState.AdjustingArea, "Suchbereich mit der Maus anpassen · Erkennung pausiert");
            return;
        }
        if (!UpdateWindow() || !CanAct(cancellation)) return;
        if (_bobberArea.IsEmpty || _phase == BotState.Fishing && (_splashArea == null || _splashArea.Value.IsEmpty))
        { Publish(_phase, "Suchbereich außerhalb von WoW · Einstellungen anpassen"); return; }
        if (HandleTimeout()) return;
        var now = Now;
        switch (_phase)
        {
            case BotState.Idle:
                var bait = owner.BaitCountdown.GetApplicationStatus();
                if (bait.Application is { } application)
                {
                    Publish(BotState.ApplyingBait, "Köder wird angebracht", application);
                    return;
                }
                if (_options.BaitEnabled && bait.ShouldApply)
                {
                    if (await desktop.SendHotkeyAsync(_game, _options.BaitHotkey, _bounds!.Value, cancellation).ConfigureAwait(false))
                    {
                        var progress = owner.BaitCountdown.StartApplication(_options.BaitDuration, bait.ManualRevision);
                        Publish(BotState.ApplyingBait, "Köder wird angebracht", progress);
                    }
                    else Publish(_phase, "Ködertaste konnte nicht gesendet werden · erneut versuchen");
                    return;
                }
                var castAt = Now;
                if (!await desktop.SendHotkeyAsync(_game, _options.Hotkey, _bounds!.Value, cancellation).ConfigureAwait(false))
                { Publish(_phase, "Wurf konnte nicht gesendet werden · erneut versuchen"); return; }
                _castAt = castAt; _searchAt = null; ResetObservation(); _phase = BotState.Casting;
                _bobberReadyAt = castAt.AddMilliseconds(FishingDefaults.CastSettlingMilliseconds);
                Publish(_phase, "Angel auswerfen · auf das Aufsetzen des Schwimmers warten");
                return;
            case BotState.Casting:
                if (now < _bobberReadyAt)
                {
                    Publish(_phase, "Angel auswerfen · auf das Aufsetzen des Schwimmers warten");
                    return;
                }
                _phase = BotState.SearchingBobber;
                _searchAt = now;
                break;
            case BotState.Looting:
                if (now >= _lootAt.AddMilliseconds(_options.LootDelayMilliseconds))
                {
                    _phase = BotState.Idle; _castAt = _searchAt = null;
                    Publish(_phase, "Wartezeit beendet · erneut auswerfen");
                }
                else Publish(_phase, "Biss erkannt · Loot angefordert · kurze Wartezeit vor dem nächsten Wurf");
                return;
        }
        Publish(_phase, _phase == BotState.SearchingBobber ? _confirmation.Activity :
            _motionReady ? "Bewegung vergleichen" : "Bisserkennung kalibrieren");
        await ScanAsync(cancellation).ConfigureAwait(false);
    }

    private async Task ScanAsync(CancellationToken cancellation)
    {
        if (!CanAct(cancellation)) return;
        int interactionVersion = owner.AreaEditVersion;
        bool splash = _phase == BotState.Fishing;
        var area = splash ? _splashArea!.Value : _bobberArea;
        var frame = desktop.Capture(area);
        if (!CanContinueScan(interactionVersion, cancellation)) return;
        // The raw image is still usable with new thresholds, but never with a changed region.
        if (ApplyOptions(beforeAnalysis: true)) return;
        var match = splash ? null : owner.Detector.Find(frame, _options.BobberMatchMinimum, cancellation, _options.BobberColorTolerance);
        var analysis = splash ? _motion.Analyze(frame, Now, cancellation,
            createPreview: owner.HasPreview && owner.LivePreviewEnabled, previewOnTrigger: owner.HasPreview,
            latchTrigger: false) : null;
        if (!CanContinueScan(interactionVersion, cancellation)) return;
        // A result from an old region or calibration must never produce an input.
        if (ApplyOptions()) return;
        if (!CanContinueScan(interactionVersion, cancellation)) return;
        if (HandleTimeout()) return;
        var confirmed = splash ? null : _confirmation.Observe(match, Now,
            TimeSpan.FromMilliseconds(Math.Max(750, _options.ScanIntervalMilliseconds * 3 + 250)));
        if (owner.HasPreview && (owner.LivePreviewEnabled || confirmed != null || analysis?.Measurement.NewTrigger == true))
            owner.Preview(analysis?.Preview ?? (match == null ? frame : BobberPreview.Mark(frame, match, confirmed != null)),
                splash ? PreviewKind.Splash : PreviewKind.Bobber, analysis?.Measurement.NewTrigger ?? confirmed != null);
        if (!CanContinueScan(interactionVersion, cancellation)) return;
        if (ApplyOptions()) return;
        if (confirmed != null) FoundBobber(area, confirmed);
        else if (analysis != null) await ObserveSplashAsync(analysis.Measurement, cancellation).ConfigureAwait(false);
        else Publish(_phase, _confirmation.Activity);
    }

    private void FoundBobber(PixelRect area, BobberMatch match)
    {
        CompleteStep(true);
        _splashAt = Now;
        _bobber = new(area.X + match.Point.X, area.Y + match.Point.Y);
        _splashArea = _options.SplashRegion.Around(_bobber.Value, _bounds!.Value);
        _confirmation.Reset();
        _motion.Reset(); _motionReady = false; _phase = BotState.Fishing;
        Publish(_phase, "Bisserkennung kalibrieren");
    }

    private async Task ObserveSplashAsync(SplashMeasurement measurement, CancellationToken cancellation)
    {
        _motionReady = measurement.Ready;
        if (measurement.SceneChanged)
        {
            ResetObservation(reacquire: true);
            Publish(_phase, "Bildwechsel · Schwimmer erneut suchen");
        }
        else if (measurement.NewTrigger && _bobber is { } point && CanAct(cancellation))
        {
            CompleteStep(true);
            _phase = BotState.Looting;
            if (!await desktop.LootAsync(_game, point, _bounds!.Value, cancellation).ConfigureAwait(false))
            {
                _phase = BotState.SearchingBobber; ResetObservation(reacquire: true);
                Publish(_phase, "Loot-Eingabe fehlgeschlagen · Schwimmer erneut suchen");
                return;
            }
            _lootAt = Now;
            ResetObservation();
            Publish(_phase, "Biss erkannt · Loot angefordert · kurze Wartezeit vor dem nächsten Wurf");
        }
    }

    private bool CanAct(CancellationToken cancellation) => !cancellation.IsCancellationRequested && !owner.IsAreaEditing && _game != 0 &&
        desktop.FindGame() == _game && desktop.IsAvailable(_game) && _bounds != null && desktop.GetGameBounds(_game) == _bounds;

    private void ResetObservation(bool reacquire = false)
    {
        _confirmation.Reset();
        _splashArea = null; _splashAt = null; _bobber = null; _motionReady = false; _motion.Reset();
        if (reacquire && _phase is (BotState.SearchingBobber or BotState.Fishing))
        { _phase = BotState.SearchingBobber; _searchAt = Now; }
    }

    private void ApplyAreaEdits()
    {
        int interaction = owner.AreaEditVersion;
        if (_areaEditVersion != interaction)
        {
            _areaEditVersion = interaction;
            ResetObservation(reacquire: true);
        }
    }

    private bool CanContinueScan(int interaction, CancellationToken cancellation)
    {
        if (owner.AreaEditVersion != interaction)
        { ApplyAreaEdits(); return false; }
        if (CanAct(cancellation)) return true;
        if (!owner.IsAreaEditing) ResetObservation(reacquire: true);
        return false;
    }

    private bool ApplyOptions(bool beforeAnalysis = false)
    {
        if (owner.TakeOptions() is not { } incoming || incoming == _options) return false;
        var old = _options; _options = incoming;
        bool regionChanged = old.BobberRegion != incoming.BobberRegion;
        bool bobberThresholdsChanged = old.BobberMatchMinimum != incoming.BobberMatchMinimum ||
            old.BobberColorTolerance != incoming.BobberColorTolerance;
        bool splashChanged = old.Motion != incoming.Motion;
        if (splashChanged) _motion.UpdateThresholds(incoming.Motion.PixelTolerance, incoming.Motion.MinimumPercent);
        if (regionChanged)
        {
            if (_bounds is { } client) _bobberArea = incoming.BobberRegion.In(client);
            ResetObservation(reacquire: true);
        }
        return regionChanged || !beforeAnalysis &&
            (_phase == BotState.SearchingBobber && bobberThresholdsChanged || _phase == BotState.Fishing && splashChanged);
    }

    private bool HandleTimeout()
    {
        if (_castAt == null || _phase is not (BotState.Casting or BotState.SearchingBobber or BotState.Fishing)) return false;
        var now = Now;
        bool total = (now - _castAt.Value).TotalSeconds >= _options.CastTimeoutSeconds;
        bool missing = _phase == BotState.SearchingBobber && _searchAt != null &&
            (now - _searchAt.Value).TotalSeconds >= _options.BobberTimeoutSeconds;
        if (!total && !missing) return false;
        CompleteStep(false);
        _phase = BotState.Idle; _castAt = _searchAt = null; ResetObservation();
        Publish(_phase, "Timeout · erneut auswerfen"); return true;
    }
}
