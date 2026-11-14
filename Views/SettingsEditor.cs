using Fish.Configuration;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fish.Views;

internal static class SettingsEditor
{
    public static void Build(StackPanel detection, StackPanel features, BotOptions o, Action changed)
    {
        detection.Children.Clear(); features.Children.Clear();
        var scan = Group(detection, "Suchintervalle", "Geschwindigkeit und Genauigkeit", true);
        Number(scan, "Scan-Intervall", "ms", o.PerformanceScanRate, 100, 1000, v => { o.PerformanceScanRate = v; changed(); });
        var bobber = Group(detection, "Bobber", "Bildvorlage · Suchbereich relativ zur WoW-Fenstermitte", true);
        Number(bobber, "Breite", "px", o.BobberAreaWidth, 100, 3840, v => { o.BobberAreaWidth = v; changed(); });
        Number(bobber, "Höhe", "px", o.BobberAreaHeight, 100, 2160, v => { o.BobberAreaHeight = v; changed(); });
        Number(bobber, "X-Versatz", "zur Mitte · px", o.BobberAreaXOffset, -7680, 7680, v => { o.BobberAreaXOffset = v; changed(); });
        Number(bobber, "Y-Versatz", "nach oben · px", o.BobberAreaYOffset, -4320, 4320, v => { o.BobberAreaYOffset = v; changed(); });
        Number(bobber, "Mindestübereinstimmung", "%", o.BobberMatchMinimum, 10, 99, v => { o.BobberMatchMinimum = v; changed(); }, "BobberMatchMinimum");
        bobber.Children.Add(new TextBlock { Text = "Vorlage: Forever-Schwimmer. Die Suche berücksichtigt Größe, Drehung und perspektivische Verformung. Größere Werte verlangen ähnlichere Treffer. Fundbilder markieren den Schwimmer grün.", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var splash = Group(detection, "Splash", "Bildvergleich · Suchbereich relativ zum erkannten Bobber", false, "SplashSettings");
        Number(splash, "Breite", "px", o.SplashAreaWidth, 50, 1920, v => { o.SplashAreaWidth = v; changed(); });
        Number(splash, "Höhe", "px", o.SplashAreaHeight, 50, 1080, v => { o.SplashAreaHeight = v; changed(); });
        Number(splash, "X-Versatz", "zum Bobber · px", o.SplashAreaXOffset, -3840, 3840, v => { o.SplashAreaXOffset = v; changed(); });
        Number(splash, "Y-Versatz", "nach oben · px", o.SplashAreaYOffset, -2160, 2160, v => { o.SplashAreaYOffset = v; changed(); });
        Number(splash, "Pixel-Rauschtoleranz", "1–255", o.SplashPixelTolerance, 1, 255, v => { o.SplashPixelTolerance = v; changed(); }, "SplashPixelTolerance");
        Number(splash, "Mindeständerung", "%", o.SplashChangePercent, 1, 80, v => { o.SplashChangePercent = v; changed(); }, "SplashChangePercent");
        Number(splash, "Kalibrierung", "ms", o.SplashCalibrationMilliseconds, 300, 3000, v => { o.SplashCalibrationMilliseconds = v; changed(); }, "SplashCalibrationMilliseconds");
        Toggle(splash, "Normale Wasserbewegung berücksichtigen", o.SplashAdaptiveThreshold, v => { o.SplashAdaptiveThreshold = v; changed(); });
        splash.Children.Add(new TextBlock { Text = "Rot markierte Pixel zeigen Änderungen zum vorherigen Bild. Live zeigt Änderungsanteil, aktuelle Grenze und normale Bewegung. Nach einem Treffer erfolgt pro Wurf eine Loot-Anfrage am Schwimmer.", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var fishing = Group(features, "Angeln", "Taste 1 · automatisches Auswerfen", true);
        Number(fishing, "Maximale Wurfdauer", "Sekunden", o.FeaturesCastTime, 5, 25, v => { o.FeaturesCastTime = v; changed(); });
        var exit = Group(features, "WoW schließen", "60 Sekunden Countdown nach dem Auslöser", true);
        Toggle(exit, "120 Sekunden ohne Loot", o.FeaturesExitOnInactivity, v => { o.FeaturesExitOnInactivity = v; changed(); });
        Toggle(exit, "Inventar voll", o.FeaturesExitOnFullBag, v => { o.FeaturesExitOnFullBag = v; changed(); });
        Toggle(exit, "Charakter tot", o.FeaturesExitOnDeath, v => { o.FeaturesExitOnDeath = v; changed(); });
        var shutdown = Group(features, "Windows herunterfahren", "120 Sekunden Countdown nach dem Auslöser", true);
        Toggle(shutdown, "120 Sekunden ohne Loot", o.FeaturesShutdownOnInactivity, v => { o.FeaturesShutdownOnInactivity = v; changed(); });
        Toggle(shutdown, "Inventar voll", o.FeaturesShutdownOnFullBag, v => { o.FeaturesShutdownOnFullBag = v; changed(); });
        Toggle(shutdown, "Charakter tot", o.FeaturesShutdownOnDeath, v => { o.FeaturesShutdownOnDeath = v; changed(); });
    }

    private static StackPanel Group(StackPanel parent, string title, string subtitle, bool expanded, string? automationId = null)
    {
        var header = new StackPanel { Spacing = 3 };
        header.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var content = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        var expander = new Expander
        {
            Header = header, Content = content, IsExpanded = expanded,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, CornerRadius = new(10)
        };
        if (automationId != null) Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(expander, automationId);
        parent.Children.Add(expander);
        return content;
    }

    private static Grid Row(StackPanel panel, string label, string? unit = null)
    {
        var row = new Grid { ColumnSpacing = 12, MinHeight = 34 };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal, Spacing = 8 };
        text.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        if (unit != null) text.Children.Add(new TextBlock { Text = unit, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(text);
        panel.Children.Add(row);
        return row;
    }

    private static void Number(StackPanel panel, string label, string unit, int value, int min, int max, Action<int> write, string? automationId = null)
    {
        var row = Row(panel, label, unit);
        var control = new NumberBox { Value = value, Minimum = min, Maximum = max, Width = 116, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, label);
        if (automationId != null) Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(control, automationId);
        control.ValueChanged += (_, args) => { if (double.IsFinite(args.NewValue)) write((int)Math.Clamp(Math.Round(args.NewValue), min, max)); };
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
    }

    private static void Toggle(StackPanel panel, string label, bool value, Action<bool> write)
    {
        var row = Row(panel, label);
        var control = new ToggleSwitch { IsOn = value, OnContent = "", OffContent = "", MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        control.Toggled += (_, _) => write(control.IsOn);
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
    }
}
