using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Fish.Views;

internal static class ThemePalette
{
    public static (Color Background, Color Text, Color Border, Color First, Color Second) For(FrameworkElement element)
    {
        return element.ActualTheme == ElementTheme.Light
            ? (Rgb(246, 247, 249), Rgb(23, 33, 43), Rgb(192, 201, 212), Rgb(0, 112, 96), Rgb(38, 87, 170))
            : (Rgb(25, 31, 39), Rgb(225, 231, 239), Rgb(68, 81, 98), Rgb(94, 219, 197), Rgb(117, 167, 255));
    }
    private static Color Rgb(byte r, byte g, byte b) => ColorHelper.FromArgb(255, r, g, b);
}
