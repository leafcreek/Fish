namespace Fish.Core;

/// <summary>Uses the same start and deadline as the fishing cycle, including its total cast limit.</summary>
public sealed record StepProgress(BotState State, DateTimeOffset StartedAt, DateTimeOffset Deadline)
{
    public double MaximumSeconds => Math.Max(0, (Deadline - StartedAt).TotalSeconds);
    public double ElapsedSeconds(DateTimeOffset now) => Math.Clamp((now - StartedAt).TotalSeconds, 0, MaximumSeconds);
}

public sealed record StepCompletion(int Id, StepProgress Progress, DateTimeOffset CompletedAt, bool Success);
