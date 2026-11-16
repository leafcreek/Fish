using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

static class RuntimePackageTests
{
    public static void Run(string assemblyPath, Action<bool, string> check)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var image = new PEReader(stream);
        var metadata = image.GetMetadataReader();
        var resources = metadata.ManifestResources.Select(handle => metadata.GetString(metadata.GetManifestResource(handle).Name)).ToArray();
        check(resources.Contains("Fish.Bobber.colors.json"), "Produktionsassembly enthält das eingefrorene JSON-Profil");
        check(!resources.Any(name => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)),
            "Produktionsassembly enthält kein PNG und keine Bild-Testdaten");
        var types = metadata.TypeDefinitions.Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name)).ToHashSet();
        check(!new[] { "BobberTemplate", "BitmapDecoder", "ReferenceBobber", "ReferenceProfile", "TestBitmapDecoder",
            "AccentBadge", "PerformanceMonitor", "SessionMetrics", "UpdateService", "TrayIcon" }.Any(types.Contains),
            "Produktionsassembly enthält keine Template-, Test-, Statistik-, Update- oder Tray-Klassen");
    }
}
