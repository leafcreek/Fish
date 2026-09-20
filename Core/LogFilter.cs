namespace Fish.Core;

public enum LogFilterLevel { All, Info, Success, Warning, Error, Problems }

public static class LogFilter
{
    public static bool Matches(BotLogEntry entry, string query, LogFilterLevel level, int minutes, DateTimeOffset now)
    {
        bool matchesLevel = level switch
        {
            LogFilterLevel.Info => entry.Level == LogLevel.Info,
            LogFilterLevel.Success => entry.Level == LogLevel.Success,
            LogFilterLevel.Warning => entry.Level == LogLevel.Warning,
            LogFilterLevel.Error => entry.Level == LogLevel.Error,
            LogFilterLevel.Problems => entry.Level is LogLevel.Warning or LogLevel.Error,
            _ => true
        };
        if (!matchesLevel || minutes > 0 && entry.Timestamp < now.AddMinutes(-minutes)) return false;
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).All(word =>
            entry.Message.Contains(word, StringComparison.OrdinalIgnoreCase) || entry.Category.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            entry.EventId.Contains(word, StringComparison.OrdinalIgnoreCase) || entry.LevelText.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
