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
    private readonly TextBox _text;
    private readonly Button _capture;
    private readonly TextBlock _hint;
    private readonly Action<string> _changed;
    private string _value;
    private bool _recording;
    public HotkeyEditor(string value, Action<string> changed)
    {
        _value = value; _changed = changed;
        Spacing = 3;
        var row = new Grid { ColumnSpacing = 5 };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _text = new TextBox { Text = value, MinHeight = 28, FontSize = 12, Padding = new(8, 3, 8, 3),
            CornerRadius = new(6), TextAlignment = TextAlignment.Center, Foreground = Appearance.Ink, PlaceholderText = "Strg+F8" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_text, "Angeln-Hotkey");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_text, "FishingHotkey");
        _capture = new Button { Content = "Setzen", MinWidth = 0, MinHeight = 28, FontSize = 12,
            Padding = new(9, 3, 9, 3), CornerRadius = new(6), Foreground = Appearance.Detection };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_capture, "Angeln-Tastenkombination setzen");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_capture, "CaptureFishingHotkey");
        _hint = new TextBlock { Text = "Taste oder Kombination mit Strg, Alt und Umschalt.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        row.Children.Add(_text); Grid.SetColumn(_capture, 1); row.Children.Add(_capture); Children.Add(row); Children.Add(_hint);
        _text.TextChanged += (_, _) =>
        {
            if (FishingHotkey.TryParse(_text.Text, out var hotkey))
            {
                _hint.Text = $"{hotkey.DisplayName} · mit Eingabe oder Fokuswechsel übernehmen.";
                _hint.Visibility = Visibility.Collapsed;
            }
            else { _hint.Text = "Ungültige Taste. Bisherige Belegung bleibt."; _hint.Visibility = Visibility.Visible; }
        };
        _text.LostFocus += (_, _) => Commit();
        _text.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == VirtualKey.Escape) { _text.Text = _value; e.Handled = true; }
        };
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
        _text.Text = hotkey.ToString(); Commit(); StopRecording();
    }
    private void Commit()
    {
        if (!FishingHotkey.TryParse(_text.Text, out var hotkey)) return;
        string value = hotkey.ToString();
        if (value != _value) { _value = value; _changed(value); }
        _hint.Text = $"Angeln: {hotkey.DisplayName} · gilt ab dem nächsten Wurf.";
    }
    private void StopRecording()
    {
        _recording = false; _capture.Content = "Setzen";
        bool valid = FishingHotkey.TryParse(_text.Text, out var hotkey);
        _hint.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        _hint.Text = valid ?
            $"Angeln: {hotkey.DisplayName} · gilt ab dem nächsten Wurf." : "Ungültige Taste. Bisherige Belegung bleibt.";
    }
}
