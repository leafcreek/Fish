using Fish.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Fish.Views;

public sealed class DetectionPreviews : Grid
{
    private readonly Image[] _images = new Image[3];
    private readonly FontIcon[] _placeholders = new FontIcon[3];
    private long _lastFrame;

    public DetectionPreviews()
    {
        ColumnSpacing = RowSpacing = 8;
        Padding = new(0, 4, 0, 10);
        MinHeight = 380;
        ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new());
        RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        RowDefinitions.Add(new() { Height = new(1.4, GridUnitType.Star) });
        AddPreview(0, "Schwimmer", "BobberPreviewImage", 0, 0, 1);
        AddPreview(1, "Biss", "SplashPreviewImage", 0, 1, 1);
        AddPreview(2, "Live", "LivePreviewImage", 1, 0, 2);
    }

    private void AddPreview(int index, string title, string id, int row, int column, int span)
    {
        var content = new Grid { RowSpacing = 8 };
        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        content.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        heading.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 5, Height = 5,
            Fill = index == 1 ? Appearance.Amber : Appearance.Detection, VerticalAlignment = VerticalAlignment.Center });
        heading.Children.Add(new TextBlock { Text = title, FontSize = 12, Foreground = Appearance.Ink,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(heading);
        _images[index] = new Image { Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        AutomationProperties.SetName(_images[index], title + "-Bild");
        AutomationProperties.SetAutomationId(_images[index], id);
        var imagePanel = new Grid();
        _placeholders[index] = new FontIcon { Glyph = "\uE714",
            FontSize = 28, Opacity = .35, Foreground = index == 1 ? Appearance.Amber : Appearance.Detection,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false };
        AutomationProperties.SetAccessibilityView(_placeholders[index], Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        imagePanel.Children.Add(_placeholders[index]); imagePanel.Children.Add(_images[index]);
        var canvas = new Border { CornerRadius = new(6), Background =
            (Brush)Application.Current.Resources["FishPreviewBackground"], Child = imagePanel };
        SetRow(canvas, 1); content.Children.Add(canvas);
        var card = new Border { CornerRadius = new(10), Padding = new(10), BorderThickness = new(1),
            BorderBrush = (Brush)Application.Current.Resources["FishStroke"],
            Background = (Brush)Application.Current.Resources["FishSurface"], Child = content };
        SetRow(card, row); SetColumn(card, column); SetColumnSpan(card, span); Children.Add(card);
    }

    public void Reset()
    {
        _lastFrame = 0;
        for (int i = 0; i < _images.Length; i++)
        { _images[i].Source = null; _placeholders[i].Visibility = Visibility.Visible; }
    }

    public void ClearLive()
    { _images[2].Source = null; _placeholders[2].Visibility = Visibility.Visible; }

    public void Refresh(PreviewBuffer buffer)
    {
        var frames = buffer.Take();
        if (frames.Live != null) _lastFrame = Environment.TickCount64;
        else if (Environment.TickCount64 - _lastFrame > 1000) ClearLive();
        Show(0, frames.Bobber); Show(1, frames.Splash); Show(2, frames.Live);
    }

    private void Show(int index, PixelFrame? frame)
    {
        if (frame == null) return;
        _placeholders[index].Visibility = Visibility.Collapsed;
        var bitmap = _images[index].Source as WriteableBitmap;
        if (bitmap == null || bitmap.PixelWidth != frame.Width || bitmap.PixelHeight != frame.Height)
            _images[index].Source = bitmap = new WriteableBitmap(frame.Width, frame.Height);
        using var stream = bitmap.PixelBuffer.AsStream();
        stream.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(frame.Pixels.AsSpan()));
        bitmap.Invalidate();
    }
}
