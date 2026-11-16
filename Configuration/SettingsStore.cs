using System.Text.Json;

namespace Fish.Configuration;

public sealed class SettingsStore
{
    public string Folder { get; }
    // Full self-extraction can place BaseDirectory in the bundle cache; use the apphost path.
    public static string DefaultFolder => Environment.ProcessPath is { } executable &&
        !string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(executable)! : AppContext.BaseDirectory;
    public SettingsStore(string? folder = null) => Folder = Path.GetFullPath(folder ?? DefaultFolder);
    public BotOptions Load() => File.Exists(Path.Combine(Folder, "appsettings.json"))
        ? Read(Path.Combine(Folder, "appsettings.json")) : new();
    public void Save(BotOptions options) => Write(Path.Combine(Folder, "appsettings.json"), options);
    private static BotOptions Read(string path)
    {
        var json = File.ReadAllText(path);
        var options = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.BotOptions)
            ?? throw new InvalidDataException("Die Einstellungsdatei ist leer.");
        if (options.SchemaVersion > BotOptions.CurrentSchemaVersion) throw new InvalidDataException("Diese Einstellungen stammen aus einer neueren Fish-Version.");
        if (options.SchemaVersion < 5)
        {
            options.WindowWidth = BotOptions.DefaultWindowWidth; options.WindowHeight = BotOptions.DefaultWindowHeight;
            options.WindowDpi = 96;
            options.WindowMaximized = false;
        }
        if (options.SchemaVersion < 6)
        {
            double scale = Math.Max(96, options.WindowDpi) / 96.0;
            if (!options.WindowMaximized && options.WindowWidth == (int)Math.Round(480 * scale) &&
                options.WindowHeight == (int)Math.Round(500 * scale))
                options.WindowHeight = (int)Math.Round(BotOptions.DefaultWindowHeight * scale);
            // The previous independent settings become the midpoint of the new controls.
            options.BobberSensitivity = options.SplashSensitivity = 50;
        }
        options.SchemaVersion = BotOptions.CurrentSchemaVersion;
        options.Normalize();
        return options;
    }
    private static void Write(string path, BotOptions options)
    {
        var copy = options.Copy();
        copy.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(copy, SettingsJsonContext.Default.BotOptions));
            if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
            // Same-directory rename; unlike ReplaceFile, this does not need to merge ACL metadata.
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
