using System.Text.Json;

namespace Fish.Configuration;

/// <summary>Read-only compatibility for the removed independent detection controls.</summary>
internal static class SettingsMigration
{
    public static void Apply(BotOptions options, string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = document.RootElement;
        int Value(string name, int fallback, int minimum, int maximum)
        {
            foreach (var property in root.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out int value)
                        ? Math.Clamp(value, minimum, maximum) : fallback;
            return fallback;
        }
        int version = Value("SchemaVersion", 0, int.MinValue, int.MaxValue);
        if (version >= 7) return;
        if (version < 6) options.BobberSensitivity = options.SplashSensitivity = 50;
        int rating = Value("BobberMatchMinimum", 50, 1, 99), color = Value("BobberColorTolerance", 50, 0, 255);
        int noise = Value("SplashPixelTolerance", 50, 1, 255), change = Value("SplashChangePercent", 10, 1, 80);
        if (rating != 50 || color != 50)
        {
            int level = options.BobberSensitivity;
            double desiredRating = DetectionSensitivity.Map(level, rating, Math.Max(95, rating), 1);
            double desiredColor = DetectionSensitivity.Map(level, color, 0, Math.Max(90, color));
            options.BobberSensitivity = Closest(level, candidate =>
            {
                var tuning = DetectionSensitivity.Bobber(new() { BobberSensitivity = candidate });
                return Math.Pow((tuning.MinimumRating - desiredRating) / 94, 2) + Math.Pow((tuning.ColorTolerance - desiredColor) / 90, 2);
            });
        }
        if (noise != 50 || change != 10)
        {
            int level = options.SplashSensitivity;
            double desiredNoise = DetectionSensitivity.Map(level, noise, Math.Max(100, noise), Math.Min(10, noise));
            double desiredChange = DetectionSensitivity.Map(level, change, Math.Max(25, change), 1);
            options.SplashSensitivity = Closest(level, candidate =>
            {
                var tuning = DetectionSensitivity.Splash(new() { SplashSensitivity = candidate });
                return Math.Pow((tuning.PixelTolerance - desiredNoise) / 90, 2) + Math.Pow((tuning.MinimumChangePercent - desiredChange) / 24, 2);
            });
        }
    }

    private static int Closest(int previous, Func<int, double> distance) => Enumerable.Range(0, 101)
        .OrderBy(distance).ThenBy(level => Math.Abs(level - previous)).First();
}
