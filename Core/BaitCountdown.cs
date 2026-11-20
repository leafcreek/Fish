namespace Fish.Core;

public sealed class BaitCountdown(TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan ApplicationTime = TimeSpan.FromSeconds(5);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly object _gate = new();
    private (long Timestamp, StepProgress Progress)? _application;
    private (long Timestamp, TimeSpan Duration, TimeSpan Delay)? _timer;
    private int _manualRevision;

    public (StepProgress? Application, TimeSpan Remaining, TimeSpan Duration) GetStatus()
    {
        lock (_gate) return StatusAt(_time.GetTimestamp());
    }

    private (StepProgress? Application, TimeSpan Remaining, TimeSpan Duration) StatusAt(long now)
    {
        var application = _application is { } pending && _time.GetElapsedTime(pending.Timestamp, now) < ApplicationTime
            ? pending.Progress : null;
        if (_timer is not { } timer) return (application, TimeSpan.Zero, TimeSpan.Zero);
        var elapsed = _time.GetElapsedTime(timer.Timestamp, now);
        if (elapsed < timer.Delay) return (application, TimeSpan.Zero, timer.Duration);
        var remaining = timer.Duration - (elapsed - timer.Delay);
        return remaining > TimeSpan.Zero ? (application, remaining, timer.Duration) : (application, TimeSpan.Zero, TimeSpan.Zero);
    }

    public void ToggleManual(TimeSpan duration)
    {
        lock (_gate)
        {
            long now = _time.GetTimestamp();
            _timer = StatusAt(now).Duration > TimeSpan.Zero ? null : (now, duration, TimeSpan.Zero);
            _manualRevision++;
        }
    }

    internal (StepProgress? Application, bool ShouldApply, int ManualRevision) GetApplicationStatus()
    {
        lock (_gate)
        {
            var status = StatusAt(_time.GetTimestamp());
            return (status.Application, status.Duration == TimeSpan.Zero, _manualRevision);
        }
    }

    internal StepProgress StartApplication(TimeSpan duration, int manualRevision)
    {
        lock (_gate)
        {
            long timestamp = _time.GetTimestamp();
            var startedAt = _time.GetUtcNow();
            var progress = new StepProgress(BotState.ApplyingBait, startedAt, startedAt + ApplicationTime);
            // Keep the cast wait after sending a key, even if the user has changed the countdown in the meantime.
            _application = (timestamp, progress);
            if (manualRevision == _manualRevision)
                _timer = (timestamp, duration, ApplicationTime);
            return progress;
        }
    }
}
