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
        var hotkey = Row(panel, "Angeln-Hotkey");
        var editor = new HotkeyEditor(options.FishingHotkey, value => { options.FishingHotkey = value; changed(); }) { Width = 210 };
        Grid.SetColumn(editor, 1); hotkey.Children.Add(editor);

        var tolerance = new StackPanel { Spacing = 4, Margin = new(0, 4, 0, 0) };
        tolerance.Children.Add(new TextBlock { Text = "Erkennungstoleranz", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(tolerance);
        Sensitivity(tolerance, "Schwimmer", "BobberSensitivity", options.BobberSensitivity,
            value => options.BobberSensitivity = value, changed);
        Sensitivity(tolerance, "Spritzer", "SplashSensitivity", options.SplashSensitivity,
            value => options.SplashSensitivity = value, changed);
    }

    private static Grid Row(StackPanel panel, string label)
    {
        var row = new Grid { ColumnSpacing = 8, MinHeight = 32 };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(row);
        return row;
    }

    private static void Sensitivity(StackPanel panel, string label, string id, int value,
        Action<int> write, Action changed)
    {
        var section = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var header = new Grid();
        header.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        header.Children.Add(new TextBlock { Text = "Gering", FontSize = 11, Opacity = .75 });
        var title = new TextBlock { Text = label, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(title, 1); header.Children.Add(title);
        var high = new TextBlock { Text = "Hoch", FontSize = 11, Opacity = .75, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(high, 2); header.Children.Add(high);
        var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, SmallChange = 1, LargeChange = 10,
            Value = value, HorizontalAlignment = HorizontalAlignment.Stretch, IsThumbToolTipEnabled = false };
        AutomationProperties.SetName(slider, label + "-Toleranz");
        AutomationProperties.SetAutomationId(slider, id);
        AutomationProperties.SetHelpText(slider, "Links geringe Toleranz, rechts hohe Toleranz.");
        slider.ValueChanged += (_, args) =>
        {
            int level = (int)Math.Clamp(Math.Round(args.NewValue), 0, 100);
            write(level);
            changed();
        };
        section.Children.Add(header); section.Children.Add(slider); panel.Children.Add(section);
    }
}
