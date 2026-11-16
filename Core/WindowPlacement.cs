namespace Fish.Core;

public static class WindowPlacement
{
    public static PixelRect Fit(PixelRect saved, IReadOnlyList<PixelRect> workAreas, PixelRect fallback,
        int minimumWidth = Configuration.BotOptions.MinimumWindowWidth, int minimumHeight = Configuration.BotOptions.MinimumWindowHeight,
        PixelRect? anchor = null)
    {
        // Resizing must stay on the current monitor, even when the larger target overlaps another one more.
        var reference = anchor ?? saved;
        var work = workAreas.OrderByDescending(area => Overlap(reference, area)).FirstOrDefault();
        bool visible = work.Width > 0 && Overlap(reference, work) > 0;
        if (!visible) work = fallback;
        int width = Math.Clamp(saved.Width, Math.Min(minimumWidth, work.Width), work.Width);
        int height = Math.Clamp(saved.Height, Math.Min(minimumHeight, work.Height), work.Height);
        return new(visible ? Math.Clamp(saved.X, work.X, work.X + work.Width - width) : work.X + (work.Width - width) / 2,
            visible ? Math.Clamp(saved.Y, work.Y, work.Y + work.Height - height) : work.Y + (work.Height - height) / 2, width, height);
    }
    private static long Overlap(PixelRect a, PixelRect b) =>
        Math.Max(0, Math.Min((long)a.X + a.Width, (long)b.X + b.Width) - Math.Max(a.X, b.X)) *
        Math.Max(0, Math.Min((long)a.Y + a.Height, (long)b.Y + b.Height) - Math.Max(a.Y, b.Y));
}
