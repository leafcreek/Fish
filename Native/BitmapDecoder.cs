using Fish.Core;
using System.Runtime.InteropServices;

namespace Fish.Native;

/// <summary>Decode the embedded PNG through Windows GDI+, preserving straight ARGB and its alpha mask.</summary>
internal static class BitmapDecoder
{
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInput
    {
        public uint Version;
        public nint DebugCallback;
        public int SuppressBackgroundThread, SuppressExternalCodecs;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int X, Y, Width, Height; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapData
    {
        public uint Width, Height;
        public int Stride, PixelFormat;
        public nint Pixels, Reserved;
    }
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdiplusStartup(out nint token, ref StartupInput input, nint output);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern void GdiplusShutdown(nint token);
    [DllImport("shlwapi.dll", ExactSpelling = true)] private static extern nint SHCreateMemStream(byte[] data, uint length);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipCreateBitmapFromStream(nint stream, out nint bitmap);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipGetImageWidth(nint image, out uint width);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipGetImageHeight(nint image, out uint height);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipBitmapLockBits(nint bitmap, ref Rect rect, uint flags, int format, out BitmapData data);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipBitmapUnlockBits(nint bitmap, ref BitmapData data);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipDisposeImage(nint image);

    public static PixelFrame Read(Stream source)
    {
        using var bytes = new MemoryStream();
        source.CopyTo(bytes);
        byte[] content = bytes.ToArray();
        var input = new StartupInput { Version = 1 };
        Check(GdiplusStartup(out var token, ref input, 0));
        nint stream = 0, bitmap = 0;
        var data = new BitmapData();
        bool locked = false;
        try
        {
            stream = SHCreateMemStream(content, checked((uint)content.Length));
            if (stream == 0) throw new InvalidDataException("PNG-Bildvorlage konnte nicht geöffnet werden.");
            Check(GdipCreateBitmapFromStream(stream, out bitmap));
            Check(GdipGetImageWidth(bitmap, out uint width));
            Check(GdipGetImageHeight(bitmap, out uint height));
            if (width is < 2 or > 8192 || height is < 2 or > 8192)
                throw new InvalidDataException("Ungültige Größe der Bildvorlage.");
            var rect = new Rect { Width = (int)width, Height = (int)height };
            const int straightArgb32 = 0x26200a;
            Check(GdipBitmapLockBits(bitmap, ref rect, 1, straightArgb32, out data));
            locked = true;
            var pixels = new int[checked((int)(width * height))];
            for (int y = 0; y < (int)height; y++)
                Marshal.Copy(data.Pixels + y * data.Stride, pixels, y * (int)width, (int)width);
            return new((int)width, (int)height, pixels);
        }
        finally
        {
            if (locked) GdipBitmapUnlockBits(bitmap, ref data);
            if (bitmap != 0) GdipDisposeImage(bitmap);
            if (stream != 0) Marshal.Release(stream);
            GdiplusShutdown(token);
        }
    }

    private static void Check(int status)
    {
        if (status != 0) throw new InvalidDataException($"PNG-Bildvorlage konnte nicht gelesen werden (GDI+ {status}).");
    }
}
