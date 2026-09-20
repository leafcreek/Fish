namespace Fish.Core;

public sealed record PerformanceSnapshot(TimeSpan Duration, bool Running, int Casts, int Loots, int SuccessfulCasts,
    int Timeouts, double CastsPerMinute, double LootsPerMinute, double? SuccessPercent,
    double? AverageCatchSeconds, double? ScanMilliseconds, int NoFish = 0);

/// <summary>Independent of the visible log. Engine writes and UI snapshots are synchronized.</summary>
public sealed class SessionMetrics
{
    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _casts = new(), _loots = new();
    private readonly Queue<(DateTimeOffset At, double Milliseconds)> _scans = new();
    private DateTimeOffset? _start, _end, _pendingCast;
    private int _castCount, _lootCount, _successfulCasts, _timeouts, _noFish;
    private double _catchSeconds;

    public void Record(BotLogEntry entry)
    {
        lock (_gate)
        {
            if (entry.EventId == "session.started")
            {
                _start = entry.Timestamp; _end = _pendingCast = null;
                _castCount = _lootCount = _successfulCasts = _timeouts = _noFish = 0;
                _catchSeconds = 0;
                _casts.Clear(); _loots.Clear(); _scans.Clear();
            }
            if (_start == null || _end != null) return;
            switch (entry.EventId)
            {
                case "cast.requested":
                    _castCount++; _casts.Enqueue(entry.Timestamp); _pendingCast = entry.Timestamp;
                    break;
                case "loot.confirmed":
                    _lootCount++; _loots.Enqueue(entry.Timestamp);
                    if (_pendingCast is { } cast)
                    {
                        _successfulCasts++;
                        _catchSeconds += Math.Max(0, (entry.Timestamp - cast).TotalSeconds);
                        _pendingCast = null;
                    }
                    break;
                case "cast.timeout": _timeouts++; _pendingCast = null; break;
                case "cast.no-fish": _noFish++; _pendingCast = null; break;
                case "learn.limit":
                case "window.changed": _pendingCast = null; break;
                case "session.ended": _end = entry.Timestamp; break;
            }
            Prune(entry.Timestamp);
        }
    }

    public void RecordScan(DateTimeOffset at, double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0) return;
        lock (_gate)
        {
            if (_start == null || _end != null) return;
            _scans.Enqueue((at, milliseconds));
            Prune(at);
        }
    }

    private void Prune(DateTimeOffset at)
    {
        while (_casts.TryPeek(out var cast) && cast <= at.AddSeconds(-60)) _casts.Dequeue();
        while (_loots.TryPeek(out var loot) && loot <= at.AddSeconds(-60)) _loots.Dequeue();
        while (_scans.TryPeek(out var scan) && scan.At <= at.AddSeconds(-5)) _scans.Dequeue();
    }

    public PerformanceSnapshot Snapshot(DateTimeOffset now)
    {
        lock (_gate)
        {
            var at = _end ?? now;
            Prune(at);
            var duration = _start is { } start ? at - start : TimeSpan.Zero;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            // During the first minute, extrapolate only the time actually observed.
            double minutes = Math.Min(60, duration.TotalSeconds) / 60;
            return new(duration, _start != null && _end == null, _castCount, _lootCount, _successfulCasts, _timeouts,
                minutes > 0 ? _casts.Count / minutes : 0, minutes > 0 ? _loots.Count / minutes : 0,
                _castCount > 0 ? 100.0 * _successfulCasts / _castCount : null,
                _successfulCasts > 0 ? _catchSeconds / _successfulCasts : null,
                _scans.Count > 0 ? _scans.Average(s => s.Milliseconds) : null, _noFish);
        }
    }
}
