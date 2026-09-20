using System.Runtime.InteropServices;
using System.Text;
using Fish.Core;

namespace Fish.Services;

public static class DiagnosticsService
{
    public static string Redact(string text)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? text : text.Replace(home, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
    }
    public static string Report(IEnumerable<BotLogEntry> log, PerformanceSnapshot stats, string? error)
    {
        var result = new StringBuilder();
        result.AppendLine($"Fish {UpdateService.CurrentVersion} · Diagnose · {DateTimeOffset.Now:O}");
        result.AppendLine($"System: {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; .NET {Environment.Version}");
        result.AppendLine($"Sitzung: {stats.Duration}; Würfe {stats.Casts}; Loots {stats.Loots}; Timeouts {stats.Timeouts}");
        result.AppendLine("Letzter Fehler:"); result.AppendLine(error ?? "Keiner erfasst.");
        result.AppendLine("Protokoll (älteste zuerst):");
        foreach (var entry in log.Reverse()) result.AppendLine(entry.ToString());
        return Redact(result.ToString());
    }
    public static string Write(string folder, string report)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"Fish-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, Redact(report), Encoding.UTF8);
        foreach (var old in new DirectoryInfo(folder).GetFiles("Fish-*.txt").OrderByDescending(f => f.LastWriteTimeUtc).Skip(20))
            try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return path;
    }
}
