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
    private long _lastFrame;

    public DetectionPreviews()
    {
        ColumnSpacing = RowSpacing = 10;
        Padding = new(4);
        MinHeight = 380;
        ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new());
        RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        RowDefinitions.Add(new() { Height = new(1.4, GridUnitType.Star) });
        AddPreview(0, "Schwimmer", "BobberPreviewImage", 0, 0, 1);
        AddPreview(1, "Spritzer", "SplashPreviewImage", 0, 1, 1);
        AddPreview(2, "Live", "LivePreviewImage", 1, 0, 2);
    }

    private void AddPreview(int index, string title, string id, int row, int column, int span)
    {
        var content = new Grid { RowSpacing = 6 };
        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        content.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        content.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _images[index] = new Image { Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        AutomationProperties.SetName(_images[index], title + "-Bild");
        AutomationProperties.SetAutomationId(_images[index], id);
        var canvas = new Border { CornerRadius = new(5), Background =
            (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"], Child = _images[index] };
        SetRow(canvas, 1); content.Children.Add(canvas);
        var card = new Border { CornerRadius = new(7), Padding = new(8), BorderThickness = new(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"], Child = content };
        SetRow(card, row); SetColumn(card, column); SetColumnSpan(card, span); Children.Add(card);
    }

    public void Reset()
    {
        _lastFrame = 0;
        foreach (var image in _images) image.Source = null;
    }

    public void ClearLive() => _images[2].Source = null;

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
        var bitmap = _images[index].Source as WriteableBitmap;
        if (bitmap == null || bitmap.PixelWidth != frame.Width || bitmap.PixelHeight != frame.Height)
            _images[index].Source = bitmap = new WriteableBitmap(frame.Width, frame.Height);
        using var stream = bitmap.PixelBuffer.AsStream();
        stream.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(frame.Pixels.AsSpan()));
        bitmap.Invalidate();
    }
}
