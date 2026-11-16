using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Fish.Views;

internal static class Appearance
{
    public static Color Background { get; } = Color.FromArgb(255, 246, 247, 249);
    public static Color Text { get; } = Color.FromArgb(255, 23, 33, 43);
    public static SolidColorBrush Running { get; } = new(Color.FromArgb(255, 16, 112, 67));
    public static SolidColorBrush Stopped { get; } = new(Color.FromArgb(255, 183, 40, 64));
    public static SolidColorBrush Detection { get; } = new(Color.FromArgb(255, 0, 107, 119));
    public static SolidColorBrush SuccessFlash { get; } = new(Color.FromArgb(32, 16, 112, 67));
    public static SolidColorBrush TimeoutFlash { get; } = new(Color.FromArgb(32, 183, 40, 64));
    public static SolidColorBrush Transparent { get; } = new(Color.FromArgb(0, 0, 0, 0));
}
