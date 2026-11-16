using Microsoft.UI.Xaml;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private bool _livePreviewVisible;
    private bool CanShowLivePreview => PreviewSection.IsExpanded && IsMainWindowVisible;

    private void InitializePreviewLayout()
    {
        MainScroll.SizeChanged += (_, _) => ResizePreviews();
        PreviewPanel.SizeChanged += (_, _) => ResizePreviews();
        PreviewSection.RegisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.Expander.IsExpandedProperty,
            (_, _) => DispatcherQueue.TryEnqueue(ResizePreviews));
    }

    private void ResizePreviews()
    {
        if (_closing || _closeAllowed || !PreviewSection.IsExpanded || !PreviewPanel.IsLoaded || MainScroll.ViewportHeight <= 0) return;
        // Absolute content position keeps the available space independent of the current scroll offset.
        double top = PreviewPanel.TransformToVisual(MainScroll).TransformPoint(new(0, 0)).Y + MainScroll.VerticalOffset;
        PreviewPanel.Height = Math.Max(PreviewPanel.MinHeight, MainScroll.ViewportHeight - top);
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
