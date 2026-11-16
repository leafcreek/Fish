namespace Fish.Core;

/// <summary>Consecutive fresh observations of the same two-color candidate. Owned by one fishing session.</summary>
internal sealed class BobberConfirmation
{
    public const int RequiredObservations = 3;
    private BobberMatch? _pending;
    private DateTimeOffset _lastAt;
    public int Count { get; private set; }
    public string Activity => _pending == null ? "Schwimmer suchen" : $"Schwimmer bestätigen · {Count}/{RequiredObservations} Aufnahmen · {_pending.Colors}";
    public void Reset() { _pending = null; Count = 0; }

    public BobberMatch? Observe(BobberMatch? candidate, DateTimeOffset now, TimeSpan maximumGap)
    {
        if (candidate == null || candidate.FeatureCount < 2) { Reset(); return null; }
        if (!candidate.NeedsConfirmation) { Reset(); return candidate; }
        double tolerance = Math.Max(5, 12 * Math.Min(candidate.Scale, _pending?.Scale ?? candidate.Scale));
        bool same = _pending != null && candidate.Features == _pending.Features && now > _lastAt && now - _lastAt <= maximumGap &&
            candidate.Scale / _pending.Scale is >= .65 and <= 1.55 &&
            candidate.Point.DistanceSquaredTo(_pending.Point) <= tolerance * tolerance;
        Count = same ? Math.Min(RequiredObservations, Count + 1) : 1;
        _pending = candidate; _lastAt = now;
        return Count >= RequiredObservations ? candidate : null;
    }
}
