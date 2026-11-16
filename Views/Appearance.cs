using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Fish.Views;

internal static class Appearance
{
    public static Color Background { get; } = Color.FromArgb(255, 241, 246, 243);
    public static Color Text { get; } = Color.FromArgb(255, 25, 61, 60);
    public static SolidColorBrush Ink { get; } = Brush(0x193D3C);
    public static SolidColorBrush Muted { get; } = Brush(0x586F6E);
    public static SolidColorBrush Running { get; } = Brush(0x087A58);
    public static SolidColorBrush RunningHover { get; } = Brush(0x066548);
    public static SolidColorBrush RunningPressed { get; } = Brush(0x054F39);
    public static SolidColorBrush Stopped { get; } = Brush(0xB5374B);
    public static SolidColorBrush StoppedHover { get; } = Brush(0x9D293C);
    public static SolidColorBrush StoppedPressed { get; } = Brush(0x802234);
    public static SolidColorBrush Detection { get; } = Brush(0x087F73);
    public static SolidColorBrush Amber { get; } = Brush(0xA36316);
    public static SolidColorBrush AccentSoft { get; } = Brush(0xCCE7DD);
    public static SolidColorBrush AmberSoft { get; } = Brush(0xF6E8CD);
    public static SolidColorBrush Track { get; } = Brush(0xD8E4E0);
    public static SolidColorBrush White { get; } = Brush(0xFFFFFF);
    public static SolidColorBrush SuccessFlash { get; } = Brush(0x087A58, 28);
    public static SolidColorBrush TimeoutFlash { get; } = Brush(0xB5374B, 28);
    public static SolidColorBrush Transparent { get; } = Brush(0, 0);

    private static SolidColorBrush Brush(uint rgb, byte alpha = 255) =>
        new(Color.FromArgb(alpha, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
}
