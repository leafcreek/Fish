using Fish.Configuration;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private SettingsEditor? _settingsEditor;

    private const string BaitMacro = """
        #showtooltip
        /use item:6533
        /use item:6532
        /use item:7307
        /use item:6530
        /use item:6811
        /use item:6529
        /use 16
        """;

    private void InitializeSettings()
    {
        _settingsEditor = new SettingsEditor(SettingsPanel, _options, SettingsChanged, CopyBaitMacro, ToggleBaitTimer);
        RefreshBaitProgress();
    }

    private void ToggleBaitTimer()
    {
        if (_closing || !_options.BaitEnabled) return;
        _baitCountdown.ToggleManual(_options.BaitDuration);
        RefreshBaitProgress();
    }

    private void CopyBaitMacro(Button button)
    {
        try
        {
            var data = new DataPackage();
            data.SetText(BaitMacro);
            Clipboard.SetContent(data);
            Clipboard.Flush();
        }
        catch (Exception error)
        {
            ShowError("Das Makro konnte nicht in die Zwischenablage kopiert werden. " + error.Message);
            return;
        }
        ShowStatusFlyout(button, "Makro kopiert", "In der Zwischenablage", "BaitMacroStatus", autoDismiss: true);
    }

    private void SettingsChanged()
    {
        _options.Normalize();
        _activeEngine?.UpdateOptions(_options);
        RefreshBaitProgress();
        UpdateOverlays();
        ScheduleSave();
    }
    private void OverlayRegionChanged(SearchRegion region, bool completed)
    {
        if (_closing || _closeAllowed) return;
        _options.BobberAreaWidth = region.Width; _options.BobberAreaHeight = region.Height;
        _options.BobberAreaXOffset = region.XOffset; _options.BobberAreaYOffset = region.YOffset;
        _options.Normalize();
        if (completed)
        {
            _activeEngine?.UpdateOptions(_options);
        }
        ScheduleSave();
    }
}
