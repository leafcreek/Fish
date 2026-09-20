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
        Number(scan, "Pixel überspringen", "pro Schritt", o.PerformanceSkip, 0, 10, v => { o.PerformanceSkip = v; changed(); });
        var bobber = Group(detection, "Bobber", "Suchbereich relativ zur WoW-Fenstermitte", true);
        Number(bobber, "Breite", "px", o.BobberAreaWidth, 100, 3840, v => { o.BobberAreaWidth = v; changed(); });
        Number(bobber, "Höhe", "px", o.BobberAreaHeight, 100, 2160, v => { o.BobberAreaHeight = v; changed(); });
        Number(bobber, "X-Versatz", "zur Mitte · px", o.BobberAreaXOffset, -7680, 7680, v => { o.BobberAreaXOffset = v; changed(); });
        Number(bobber, "Y-Versatz", "nach oben · px", o.BobberAreaYOffset, -4320, 4320, v => { o.BobberAreaYOffset = v; changed(); });
        Number(bobber, "Farbtoleranz", "0–255", o.BobberAreaTolerance, 0, 255, v => { o.BobberAreaTolerance = v; changed(); });
        Toggle(bobber, "Toleranz automatisch lernen", o.BobberAreaAutoLearn, v => { o.BobberAreaAutoLearn = v; changed(); });
        var splash = Group(detection, "Splash", "Suchbereich relativ zum erkannten Bobber", false);
        Number(splash, "Breite", "px", o.SplashAreaWidth, 50, 1920, v => { o.SplashAreaWidth = v; changed(); });
        Number(splash, "Höhe", "px", o.SplashAreaHeight, 50, 1080, v => { o.SplashAreaHeight = v; changed(); });
        Number(splash, "X-Versatz", "zum Bobber · px", o.SplashAreaXOffset, -3840, 3840, v => { o.SplashAreaXOffset = v; changed(); });
        Number(splash, "Y-Versatz", "nach oben · px", o.SplashAreaYOffset, -2160, 2160, v => { o.SplashAreaYOffset = v; changed(); });
        Number(splash, "Farbtoleranz", "0–255", o.SplashAreaTolerance, 0, 255, v => { o.SplashAreaTolerance = v; changed(); });
        Toggle(splash, "Toleranz automatisch lernen", o.SplashAreaAutoLearn, v => { o.SplashAreaAutoLearn = v; changed(); });
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
        features.Children.Add(new TextBlock { Text = "Folgeaktionen gelten während einer gestarteten Sitzung. Stop bricht auch laufende Countdowns ab.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new(4, 4, 4, 0) });
    }

    private static StackPanel Group(StackPanel parent, string title, string subtitle, bool expanded)
    {
        var header = new StackPanel { Spacing = 3 };
        header.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        var content = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        parent.Children.Add(new Expander
        {
            Header = header, Content = content, IsExpanded = expanded,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, CornerRadius = new(10)
        });
        return content;
    }

    private static Grid Row(StackPanel panel, string label, string? unit = null)
    {
        var row = new Grid { ColumnSpacing = 12, MinHeight = 34 };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal, Spacing = 8 };
        text.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        if (unit != null) text.Children.Add(new TextBlock { Text = unit, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(text);
        panel.Children.Add(row);
        return row;
    }

    private static void Number(StackPanel panel, string label, string unit, int value, int min, int max, Action<int> write)
    {
        var row = Row(panel, label, unit);
        var control = new NumberBox { Value = value, Minimum = min, Maximum = max, Width = 116, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, $"{label} ({unit})");
        control.ValueChanged += (_, args) => { if (double.IsFinite(args.NewValue)) write((int)Math.Clamp(Math.Round(args.NewValue), min, max)); };
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
    }

    private static void Toggle(StackPanel panel, string label, bool value, Action<bool> write)
    {
        var row = Row(panel, label);
        var control = new ToggleSwitch { IsOn = value, OnContent = "", OffContent = "", MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, label);
        control.Toggled += (_, _) => write(control.IsOn);
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
    }
}
