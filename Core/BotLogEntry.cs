namespace Fish.Core;

public enum LogLevel { Info, Success, Warning, Error }

public sealed record BotLogEntry(DateTimeOffset Timestamp, LogLevel Level, string Category, string EventId, string Message)
{
    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");
    public string LevelText => Level switch
    {
        LogLevel.Success => "OK", LogLevel.Warning => "WARNUNG", LogLevel.Error => "FEHLER", _ => "INFO"
    };
    public override string ToString() => $"{Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{LevelText}] {Category} · {Message}";
}
