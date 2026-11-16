using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Fish.Services.Updates;

internal sealed record UpdatePlan(string Target, string Sha256, string OriginalSha256, int ParentId, long ParentStarted, string SettingsDirectory);

internal sealed class UpdateHandoff(string session, Process helper) : IDisposable
{
    private bool _approved;
    public void Approve()
    {
        if (helper.HasExited) throw new IOException("Der Update-Prozess wurde unerwartet beendet.");
        File.WriteAllText(Path.Combine(UpdateInstaller.SessionFolder(session), "approved"), "ok");
        _approved = true;
    }
    public void Dispose()
    {
        if (!_approved)
            File.WriteAllText(Path.Combine(UpdateInstaller.SessionFolder(session), "cancelled"), "cancel");
        helper.Dispose();
    }
}

internal static class UpdateInstaller
{
    internal static string CacheFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fish", "Updates");
    internal static string SessionFolder(string session)
    {
        if (!Guid.TryParseExact(session, "N", out _)) throw new IOException("Der Update-Auftrag ist ungültig.");
        return Path.Combine(CacheFolder, session);
    }

    public static async Task<UpdateHandoff> StartAsync(DownloadedUpdate download, string settingsDirectory, CancellationToken cancellation)
    {
        string target = Environment.ProcessPath ?? throw new IOException("Der Speicherort von Fish ist unbekannt.");
        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileNameWithoutExtension(target).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Bitte die veröffentlichte Fish.exe verwenden.");
        string folder = SessionFolder(download.Session);
        string helperPath = Path.Combine(folder, "Fish.Updater.exe");
        await Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            // Check permissions before stopping the session or closing the app.
            string probe = Path.Combine(Path.GetDirectoryName(target)!, ".fish-write-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            using var parent = Process.GetCurrentProcess();
            var plan = new UpdatePlan(target, download.Sha256, Hash(target), parent.Id, parent.StartTime.ToUniversalTime().Ticks, Path.GetFullPath(settingsDirectory));
            File.WriteAllText(Path.Combine(folder, "plan.json"), JsonSerializer.Serialize(plan));
            File.Copy(target, helperPath, false);
            cancellation.ThrowIfCancellationRequested();
        }, cancellation);
        var start = new ProcessStartInfo(helperPath) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder };
        start.ArgumentList.Add("--apply-update");
        start.ArgumentList.Add(download.Session);
        var helper = Process.Start(start) ?? throw new IOException("Der Update-Prozess konnte nicht gestartet werden.");
        var handoff = new UpdateHandoff(download.Session, helper);
        try
        {
            var elapsed = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(folder, "ready")))
            {
                cancellation.ThrowIfCancellationRequested();
                if (helper.HasExited || elapsed.Elapsed > TimeSpan.FromSeconds(20))
                    throw new IOException("Der Update-Prozess ist nicht bereit. Fish bleibt geöffnet.");
                await Task.Delay(100, cancellation);
            }
            return handoff;
        }
        catch { handoff.Dispose(); throw; }
    }

    // Runs in a copy of the old EXE, before any XAML or overlays are initialized.
    public static int RunHelper(string session)
    {
        string folder;
        try { folder = SessionFolder(session); }
        catch { return 1; }
        UpdatePlan? plan = null;
        bool parentExited = false, replaced = false;
        Process? restarted = null;
        try
        {
            string helperPath = Path.Combine(folder, "Fish.Updater.exe");
            if (!SamePath(Environment.ProcessPath!, helperPath)) throw new IOException("Ungültiger Update-Prozess.");
            plan = JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(Path.Combine(folder, "plan.json")))
                ?? throw new IOException("Der Update-Auftrag fehlt.");
            string package = Path.Combine(folder, "Fish.exe");
            if (!Path.IsPathFullyQualified(plan.Target) || !plan.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                SamePath(Path.GetDirectoryName(plan.Target)!, folder) || !Path.IsPathFullyQualified(plan.SettingsDirectory))
                throw new IOException("Der Speicherort des Updates ist ungültig.");
            using var parent = Process.GetProcessById(plan.ParentId);
            if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStarted || !SamePath(parent.MainModule!.FileName, plan.Target))
                throw new IOException("Das zu aktualisierende Fish-Fenster wurde nicht gefunden.");
            Verify(package, plan.Sha256);
            Verify(plan.Target, plan.OriginalSha256);
            File.WriteAllText(Path.Combine(folder, "ready"), "ok");
            var elapsed = Stopwatch.StartNew();
            while (!parent.WaitForExit(100))
            {
                if (File.Exists(Path.Combine(folder, "cancelled")) || elapsed.Elapsed > TimeSpan.FromSeconds(60)) return 1;
            }
            parentExited = true;
            if (!File.Exists(Path.Combine(folder, "approved")) || File.Exists(Path.Combine(folder, "cancelled"))) return 1;
            // A second Fish instance may still hold the file. Retry briefly, then leave it intact.
            elapsed.Restart();
            while (true)
            {
                try { ApplyPackage(package, plan.Target, plan.OriginalSha256, plan.Sha256); break; }
                catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(250); }
            }
            replaced = true;
            restarted = Restart(plan, session);
            elapsed.Restart();
            while (!File.Exists(Path.Combine(folder, "started")))
            {
                if (restarted.HasExited || elapsed.Elapsed > TimeSpan.FromSeconds(30))
                    throw new IOException("Die neue Version konnte nicht geöffnet werden.");
                Thread.Sleep(100);
            }
            File.Delete(package);
            return 0;
        }
        catch (Exception error)
        {
            if (plan == null || !parentExited) return 1;
            try
            {
                if (replaced)
                {
                    if (restarted is { HasExited: false }) { restarted.Kill(); restarted.WaitForExit(5000); }
                    RestoreBackup(plan.Target, plan.OriginalSha256);
                }
                File.WriteAllText(Path.Combine(folder, "error.txt"), "Das Update konnte nicht installiert werden. Die bisherige Version bleibt erhalten. " + error.Message);
                using var restored = Restart(plan, session);
            }
            catch (Exception recovery)
            {
                File.WriteAllText(Path.Combine(folder, "error.txt"), "Update fehlgeschlagen: " + error.Message + "\nWiederherstellung: " + recovery.Message);
            }
            return 1;
        }
        finally { restarted?.Dispose(); }
    }

    public static string? NotifyStartup(string[] arguments)
    {
        int index = Array.IndexOf(arguments, "--update-session");
        if (index < 0 || index + 1 >= arguments.Length) return null;
        try
        {
            string folder = SessionFolder(arguments[index + 1]);
            var plan = JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(Path.Combine(folder, "plan.json")));
            if (plan == null || !SamePath(plan.Target, Environment.ProcessPath!)) return null;
            File.WriteAllText(Path.Combine(folder, "started"), Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string error = Path.Combine(folder, "error.txt");
            return File.Exists(error) ? File.ReadAllText(error) : null;
        }
        catch { return "Das Update-Ergebnis konnte nicht gelesen werden."; }
    }

    public static void CleanOldDownloads()
    {
        try
        {
            if (!Directory.Exists(CacheFolder)) return;
            foreach (string folder in Directory.EnumerateDirectories(CacheFolder))
            {
                if (!Guid.TryParseExact(Path.GetFileName(folder), "N", out _) ||
                    (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0 ||
                    Directory.GetLastWriteTimeUtc(folder) > DateTime.UtcNow.AddDays(-2)) continue;
                foreach (string name in new[] { "Fish.exe", "Fish.Updater.exe", "plan.json", "ready", "approved", "cancelled", "started", "error.txt" })
                    File.Delete(Path.Combine(folder, name));
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void ApplyPackage(string package, string target, string originalHash, string newHash)
    {
        Verify(package, newHash);
        Verify(target, originalHash);
        string candidate = target + ".update-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(package, candidate, false);
            Verify(candidate, newHash);
            // Candidate and target are on the same volume, even when the cache is on another disk.
            File.Replace(candidate, target, target + ".bak");
        }
        finally { File.Delete(candidate); }
    }

    internal static void RestoreBackup(string target, string originalHash)
    {
        Verify(target + ".bak", originalHash);
        File.Replace(target + ".bak", target, null);
    }

    private static Process Restart(UpdatePlan plan, string session)
    {
        var start = new ProcessStartInfo(plan.Target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(plan.Target)! };
        foreach (string argument in new[] { "--settings-directory", plan.SettingsDirectory, "--update-session", session }) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new IOException("Fish konnte nicht neu gestartet werden.");
    }
    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static void Verify(string path, string expected)
    {
        if (!string.Equals(Hash(path), expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Die Datei stimmt nicht mit ihrer Prüfsumme überein.");
    }
    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
