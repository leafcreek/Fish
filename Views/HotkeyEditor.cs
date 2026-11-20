using Fish.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Fish.Views;

internal sealed class HotkeyEditor : StackPanel
{
    private readonly TextBlock _text;
    private readonly Button _capture;
    private readonly TextBlock _hint;
    private readonly Action<string> _changed;
    private bool _recording;
    public HotkeyEditor(string value, Action<string> changed, string label = "Angeln", string automationId = "FishingHotkey")
    {
        _changed = changed;
        Spacing = 3;
        var row = new Grid { ColumnSpacing = 5 };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _text = new TextBlock { Text = value, FontSize = 12, TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = Appearance.Ink };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_text, automationId);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(_text, $"{label}-Hotkey");
        var display = new Border { Child = _text, MinHeight = 28, Padding = new(8, 3, 8, 3),
            CornerRadius = new(6), BorderThickness = new(1), BorderBrush = Appearance.Track,
            Background = Appearance.White, IsHitTestVisible = false };
        _capture = new Button { Content = "Setzen", Style = (Style)Application.Current.Resources["FishSettingsButton"] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_capture, $"{label}-Tastenkombination setzen");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_capture, $"Capture{automationId}");
        _hint = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        row.Children.Add(display); Grid.SetColumn(_capture, 1); row.Children.Add(_capture); Children.Add(row); Children.Add(_hint);
        _capture.Click += (_, _) =>
        {
            _recording = true; _capture.Content = "Taste drücken …";
            _hint.Text = "Taste drücken. Esc bricht ab.";
            _hint.Visibility = Visibility.Visible;
            _capture.Focus(FocusState.Programmatic);
        };
        _capture.PreviewKeyDown += CaptureKey;
        _capture.LostFocus += (_, _) => StopRecording();
    }
    private void CaptureKey(object sender, KeyRoutedEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;
        if (e.Key == VirtualKey.Escape) { StopRecording(); return; }
        bool Down(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
        var modifiers = (Down(VirtualKey.Control) ? HotkeyModifiers.Control : 0) |
            (Down(VirtualKey.Menu) ? HotkeyModifiers.Alt : 0) | (Down(VirtualKey.Shift) ? HotkeyModifiers.Shift : 0);
        if (!FishingHotkey.TryCreate((int)e.Key, modifiers, out var hotkey)) return;
        string value = hotkey.ToString();
        if (value != _text.Text)
        {
            _text.Text = value;
            _changed(value);
        }
        StopRecording();
    }
    private void StopRecording()
    {
        _recording = false; _capture.Content = "Setzen";
        _hint.Visibility = Visibility.Collapsed;
    }
}
