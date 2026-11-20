using Fish.Configuration;

namespace Fish.Core;

/// <summary>Session lifecycle and atomic configuration handoff. Each run owns its fishing state.</summary>
public sealed class BotEngine(IBotDesktop desktop, TimeProvider? timeProvider = null, BaitCountdown? baitCountdown = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly Lazy<BobberDetector> _detector = new(() => new());
    private FishingOptions? _pendingOptions;
    private readonly object _lifecycle = new();
    private bool _running;
    private int _areaEditing;
    private int _areaEditVersion;
    private int _livePreviewEnabled = 1;
    public void SetAreaEditing(bool editing)
    {
        int value = editing ? 1 : 0;
        if (Interlocked.Exchange(ref _areaEditing, value) != value) Interlocked.Increment(ref _areaEditVersion);
    }
    internal bool IsAreaEditing => Volatile.Read(ref _areaEditing) != 0;
    internal int AreaEditVersion => Volatile.Read(ref _areaEditVersion);
    public event Action<BotUpdate>? Updated;
    public event Action<PixelFrame, PreviewKind, bool>? PreviewAvailable;

    public void UpdateOptions(BotOptions source)
    {
        var options = FishingOptions.From(source);
        lock (_lifecycle) { if (_running) Interlocked.Exchange(ref _pendingOptions, options); }
    }
    internal FishingOptions? TakeOptions() => Interlocked.Exchange(ref _pendingOptions, null);
    internal BaitCountdown BaitCountdown { get; } = baitCountdown ?? new(timeProvider);
    internal BobberDetector Detector => _detector.Value;
    internal bool HasPreview => PreviewAvailable != null;
    internal bool LivePreviewEnabled => Volatile.Read(ref _livePreviewEnabled) != 0;
    public void SetLivePreviewEnabled(bool enabled) => Volatile.Write(ref _livePreviewEnabled, enabled ? 1 : 0);
    internal void Publish(BotUpdate update) => Updated?.Invoke(update);
    internal void Preview(PixelFrame frame, PreviewKind kind, bool found)
        => PreviewAvailable?.Invoke(frame, kind, found);

    public async Task RunAsync(BotOptions source, CancellationToken cancellation)
    {
        lock (_lifecycle)
        {
            if (_running) throw new InvalidOperationException("Diese Sitzung läuft bereits.");
            _running = true;
        }
        try { await new FishingSession(this, desktop, _time, FishingOptions.From(source)).RunAsync(cancellation).ConfigureAwait(false); }
        finally
        {
            lock (_lifecycle)
            {
                _running = false; Interlocked.Exchange(ref _pendingOptions, null);
            }
        }
    }
}
