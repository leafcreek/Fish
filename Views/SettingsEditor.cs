using Fish.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Fish.Views;

internal sealed class SettingsEditor
{
    public SettingsEditor(StackPanel panel, BotOptions options, Action changed)
    {
        panel.Children.Clear();
        var hotkey = Row(panel, "Angeltaste");
        var editor = new HotkeyEditor(options.FishingHotkey, value => { options.FishingHotkey = value; changed(); }) { Width = 145 };
        Grid.SetColumn(editor, 1); hotkey.Children.Add(editor);

        var tolerance = new StackPanel { Spacing = 0, Margin = new(0, 4, 0, 0) };
        tolerance.Children.Add(new TextBlock { Text = "Erkennungstoleranz", FontSize = 13, Foreground = Appearance.Ink,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(tolerance);
        Sensitivity(tolerance, "Schwimmer", "BobberSensitivity", options.BobberSensitivity,
            value => options.BobberSensitivity = value, changed);
        Sensitivity(tolerance, "Spritzer", "SplashSensitivity", options.SplashSensitivity,
            value => options.SplashSensitivity = value, changed);
        var direction = new Grid();
        direction.Children.Add(new TextBlock { Text = "Gering", FontSize = 11, Foreground = Appearance.Muted,
            HorizontalAlignment = HorizontalAlignment.Left });
        direction.Children.Add(new TextBlock { Text = "Hoch", FontSize = 11, Foreground = Appearance.Muted,
            HorizontalAlignment = HorizontalAlignment.Right });
        tolerance.Children.Add(direction);
    }

    private static Grid Row(StackPanel panel, string label)
    {
        var row = new Grid { ColumnSpacing = 8, MinHeight = 32 };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Appearance.Muted, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(row);
        return row;
    }

    private static void Sensitivity(StackPanel panel, string label, string id, int value,
        Action<int> write, Action changed)
    {
        var section = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var accent = label == "Schwimmer" ? Appearance.Detection : Appearance.Amber;
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
        title.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 5, Height = 5, Fill = accent, VerticalAlignment = VerticalAlignment.Center });
        title.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Appearance.Ink,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, SmallChange = 1, LargeChange = 10,
            Value = value, Height = 26, MinHeight = 26, HorizontalAlignment = HorizontalAlignment.Stretch, IsThumbToolTipEnabled = false };
        foreach (string state in new[] { "", "PointerOver", "Pressed" })
        {
            slider.Resources["SliderThumbBackground" + state] = accent;
            slider.Resources["SliderTrackValueFill" + state] = accent;
            slider.Resources["SliderTrackFill" + state] = Appearance.Track;
        }
        slider.Resources["SliderHeaderForeground"] = Appearance.Ink;
        AutomationProperties.SetName(slider, label + "-Toleranz");
        AutomationProperties.SetAutomationId(slider, id);
        AutomationProperties.SetHelpText(slider, "Links geringe Toleranz, rechts hohe Toleranz.");
        slider.ValueChanged += (_, args) =>
        {
            int level = (int)Math.Clamp(Math.Round(args.NewValue), 0, 100);
            write(level);
            changed();
        };
        section.Children.Add(title); section.Children.Add(slider); panel.Children.Add(section);
    }
}
