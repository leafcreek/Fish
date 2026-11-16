using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fish.Configuration;

public sealed class SettingsStore
{
    public const string FileName = "settings.json";
    public string Folder { get; }
    private readonly string _legacyPath;
    private string SettingsPath => Path.Combine(Folder, FileName);
    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fish");

    // Full self-extraction can place BaseDirectory in the bundle cache; use the apphost path.
    private static string ExecutableFolder => Environment.ProcessPath is { } executable &&
        !string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(executable)! : AppContext.BaseDirectory;
    public SettingsStore(string? folder = null) : this(folder ?? DefaultFolder, folder ?? ExecutableFolder) { }
    internal SettingsStore(string folder, string legacyFolder)
    {
        Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        _legacyPath = Path.Combine(Path.GetFullPath(legacyFolder), "appsettings.json");
    }
    public BotOptions Load() => WithLock(() =>
    {
        if (ExistsWithBackup(SettingsPath))
        {
            var options = ReadWithBackup(SettingsPath, out bool recovered);
            if (recovered) Write(SettingsPath, options, preserveBackup: true);
            return options;
        }
        if (ExistsWithBackup(_legacyPath))
        {
            var options = ReadWithBackup(_legacyPath, out _);
            Write(SettingsPath, options);
            return options;
        }
        return new BotOptions();
    });
    public void Save(BotOptions options) => WithLock(() => { Write(SettingsPath, options); return true; });

    private static bool ExistsWithBackup(string path) => File.Exists(path) || File.Exists(path + ".bak");
    private static bool IsDamaged(Exception error) => error is JsonException or InvalidDataException;
    private static BotOptions ReadWithBackup(string path, out bool recovered)
    {
        recovered = false;
        if (File.Exists(path))
        {
            try { return Read(path); }
            catch (Exception error) when (IsDamaged(error) && File.Exists(path + ".bak")) { }
        }
        recovered = true;
        return Read(path + ".bak");
    }

    private T WithLock<T>(Func<T> action)
    {
        string name = "Local\\Fish.Settings." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Folder.ToUpperInvariant())));
        using var mutex = new Mutex(false, name);
        bool acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("Die Einstellungen werden gerade von einem anderen Fish-Fenster gespeichert.");
            return action();
        }
        finally { if (acquired) mutex.ReleaseMutex(); }
    }
    private static BotOptions Read(string path)
    {
        var json = File.ReadAllText(path);
        var options = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.BotOptions)
            ?? throw new InvalidDataException("Die Einstellungsdatei ist leer.");
        // A newer format must never be replaced with an older backup or legacy settings.
        if (options.SchemaVersion > BotOptions.CurrentSchemaVersion) throw new NotSupportedException("Diese Einstellungen stammen aus einer neueren Fish-Version.");
        SettingsMigration.Apply(options, json);
        options.SchemaVersion = BotOptions.CurrentSchemaVersion;
        options.Normalize();
        return options;
    }
    private static void Write(string path, BotOptions options, bool preserveBackup = false)
    {
        var copy = options.Copy();
        copy.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(copy, SettingsJsonContext.Default.BotOptions));
            if (File.Exists(path))
            {
                try
                {
                    _ = Read(path);
                    if (!preserveBackup) File.Copy(path, path + ".bak", overwrite: true);
                }
                catch (Exception error) when (IsDamaged(error))
                {
                    // Keep the original for recovery, and never rotate damaged data into the good backup.
                    File.Copy(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                }
            }
            // Same-directory rename; unlike ReplaceFile, this does not need to merge ACL metadata.
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
