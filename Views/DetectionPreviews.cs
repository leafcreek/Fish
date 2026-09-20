using Fish.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Fish.Views;

public sealed class DetectionPreviews : Grid
{
    private readonly Image[] _images = new Image[3];
    private readonly TextBlock[] _labels = new TextBlock[3];
    private long _lastFrame;

    public DetectionPreviews()
    {
        ColumnSpacing = 10;
        for (int i = 0; i < 3; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition());
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new AccentBadge { Label = new[] { "Bobber", "Splash", "Live" }[i], HorizontalAlignment = HorizontalAlignment.Left });
            panel.Children.Add(new TextBlock { Text = i == 2 ? "Aktueller Suchbereich" : "Letzter Fund", FontSize = 12 });
            _images[i] = new Image { Stretch = Stretch.Uniform, MaxHeight = 150, HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(_images[i]);
            _labels[i] = new TextBlock { Text = "Noch kein Bild", FontSize = 12, TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(_labels[i]);
            SetColumn(panel, i);
            Children.Add(panel);
        }
    }

    public void Reset()
    {
        for (int i = 0; i < 3; i++) { _images[i].Source = null; _labels[i].Text = "Noch kein Bild"; }
    }

    public void PauseLive()
    {
        _images[2].Source = null;
        _labels[2].Text = "Keine aktive Aufnahme";
    }

    public void Refresh(PreviewBuffer buffer)
    {
        var frames = buffer.Take();
        if (frames.Live != null) _lastFrame = Environment.TickCount64;
        else if (Environment.TickCount64 - _lastFrame > 1000) PauseLive();
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
        _labels[index].Text = DateTime.Now.ToString("HH:mm:ss");
    }
}
