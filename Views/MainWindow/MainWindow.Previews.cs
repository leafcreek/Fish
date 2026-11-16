using Microsoft.UI.Xaml;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private bool _livePreviewVisible;
    private bool _previewLayoutQueued;
    private bool CanShowLivePreview => PreviewSection.IsExpanded && IsMainWindowVisible;

    private void InitializePreviewLayout()
    {
        PreviewSection.IsExpanded = false;
        Root.Loaded += (_, _) => QueuePreviewLayout();
        MainScroll.SizeChanged += (_, _) => QueuePreviewLayout();
        SettingsCard.SizeChanged += (_, _) => QueuePreviewLayout();
        PreviewPanel.Loaded += (_, _) => QueuePreviewLayout();
        PreviewSection.RegisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.Expander.IsExpandedProperty,
            (_, _) =>
            {
                MainScroll.ChangeView(null, 0, null, disableAnimation: true);
                QueuePreviewLayout();
            });
    }

    private void QueuePreviewLayout()
    {
        if (_previewLayoutQueued || _closing || _closeAllowed) return;
        _previewLayoutQueued = DispatcherQueue.TryEnqueue(() =>
        {
            _previewLayoutQueued = false;
            if (_closing || _closeAllowed) return;
            EnsureWindowVisible();
            ResizePreviews();
        });
    }

    private void ResizePreviews()
    {
        if (_closing || _closeAllowed || !PreviewSection.IsExpanded || !PreviewPanel.IsLoaded || MainScroll.ViewportHeight <= 0) return;
        // The expander animates its content position. Use only stable layout measurements,
        // never the preview's transformed position or its own SizeChanged feedback.
        const double headerAndBorder = 40;
        double available = Math.Max(160, MainScroll.ViewportHeight - SettingsCard.ActualHeight - MainContent.Spacing - headerAndBorder);
        // On a smaller work area all three images shrink together instead of adding a scrollbar.
        double minimum = Math.Min(380, available);
        if (PreviewPanel.MinHeight != minimum) PreviewPanel.MinHeight = minimum;
        if (PreviewPanel.Height != available) PreviewPanel.Height = available;
    }

    private void RefreshPreviews()
    {
        bool visible = CanShowLivePreview;
        _activeEngine?.SetLivePreviewEnabled(visible);
        if (visible) PreviewPanel.Refresh(_previews);
        else if (_livePreviewVisible)
        {
            _previews.ClearLive();
            PreviewPanel.ClearLive();
        }
        _livePreviewVisible = visible;
    }
}
