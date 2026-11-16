using Fish.Configuration;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private void InitializeSettings()
    {
        _ = new SettingsEditor(SettingsPanel, _options, SettingsChanged);
    }
    private void SettingsChanged()
    {
        _options.Normalize();
        _activeEngine?.UpdateOptions(_options);
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
