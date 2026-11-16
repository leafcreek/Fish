int passed = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    passed++; Console.WriteLine("PASS " + message);
}
string folder = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "tests", Guid.NewGuid().ToString("N"));
if (args.Contains("--live-sliders"))
{
    try
    {
        SplashMotionTests.Run(Check);
        await BotEngineTests.Run(Check, name => name.StartsWith("Live Toleranz") ||
            name is "Einstellungen während Zweifarben-Bestätigung" or "Splash-Empfindlichkeit live ändern");
        Console.WriteLine($"{passed} Live-Regler-Prüfungen erfolgreich.");
    }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}
if (args.Contains("--updates-only"))
{
    await UpdateTests.Run(Check, folder);
    Console.WriteLine($"{passed} Update-Prüfungen erfolgreich.");
    return;
}
int packageIndex = Array.IndexOf(args, "--verify-runtime-assembly");
if (packageIndex >= 0)
{
    RuntimePackageTests.Run(args[packageIndex + 1], Check);
    Console.WriteLine($"{passed} Laufzeitpaket-Prüfungen erfolgreich.");
    return;
}
int benchmarkIndex = Array.IndexOf(args, "--benchmark-bobber");
if (benchmarkIndex >= 0)
{
    int comparisonIndex = Array.IndexOf(args, "--compare-bobber");
    BobberBenchmark.Run(args[benchmarkIndex + 1], comparisonIndex < 0 ? null : args[comparisonIndex + 1]);
    return;
}
int exportIndex = Array.IndexOf(args, "--export-bobber-colors");
if (exportIndex >= 0)
{
    var profile = ReferenceProfile.Measure(ReferenceBobber.Default);
    File.WriteAllText(args[exportIndex + 1], System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Blau {profile.Blue.Hex} · Rot {profile.Red.Hex} · Körper {profile.Body.Hex} · Metall {profile.Metal.Hex}");
    return;
}
if (args.Contains("--color-only"))
{
    ColorBobberTests.Run(Check, args);
    Console.WriteLine($"{passed} Farbgruppen-Prüfungen erfolgreich.");
    return;
}
if (args.Contains("--bobber-environments"))
{
    try
    {
        BobberEnvironmentTests.Run(Check);
        ColorBobberTests.Run(Check, args);
        BobberColorToleranceTests.Run(Check);
        Console.WriteLine($"{passed} Schwimmer-Umgebungsprüfungen erfolgreich.");
    }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}
if (args.Contains("--overlay-only"))
{
    try
    {
        OverlayDesktopTests.Run(Check);
        Console.WriteLine($"{passed} native Overlay-Prüfungen erfolgreich.");
    }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}
PreviewBufferTests.Run(Check);
SplashMotionTests.Run(Check);
HotkeyTests.Run(Check);
ReferenceProfileTests.Run(Check);
BobberEnvironmentTests.Run(Check);
ColorBobberTests.Run(Check, args);
BobberColorToleranceTests.Run(Check);
BobberConfirmationTests.Run(Check);
GeometryTests.Run(Check);
RegionEditTests.Run(Check);
SettingsTests.Run(Check, folder);
SensitivityTests.Run(Check, folder);
await BotEngineTests.Run(Check);
DesktopTests.Run(Check, args);
if (args.Contains("--overlay-smoke")) OverlayDesktopTests.Run(Check);
Console.WriteLine($"{passed} Prüfungen erfolgreich. Testdateien: {folder}");
