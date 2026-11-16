using System.ComponentModel;
using System.Runtime.InteropServices;
using Fish.Core;

namespace Fish.Native;

internal static class KeyboardInput
{
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct Payload
    {
        [FieldOffset(0)] public Keyboard Keyboard;
        [FieldOffset(0)] public Mouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public Payload Payload; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    // Avoid changing a combination while the user is holding a modifier or the cast key.
    internal static bool UserIsTyping(FishingHotkey hotkey)
    {
        if ((GetAsyncKeyState(hotkey.Key) & 0x8000) != 0) return true;
        foreach (int key in (ReadOnlySpan<int>)[0x10, 0x11, 0x12, 0x5b, 0x5c])
            if ((GetAsyncKeyState(key) & 0x8000) != 0) return true;
        return false;
    }

    internal static void Send(FishingHotkey hotkey, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var keys = hotkey.PressedKeys();
        Input Key(int key, bool up) => new() { Type = 1, Payload = new() { Keyboard = new() {
            Key = (ushort)key, Flags = (up ? 2u : 0) | (IsExtended(key) ? 1u : 0) } } };
        var batch = keys.Select(key => Key(key, false)).Concat(keys.Reverse().Select(key => Key(key, true))).ToArray();
        // One batch keeps key-up paired with key-down even if Stop arrives in between.
        if (SendInput((uint)batch.Length, batch, Marshal.SizeOf<Input>()) == batch.Length) return;
        int error = Marshal.GetLastWin32Error();
        var release = keys.Reverse().Select(key => Key(key, true)).ToArray();
        SendInput((uint)release.Length, release, Marshal.SizeOf<Input>());
        throw new Win32Exception(error, "Der Angeln-Hotkey konnte nicht vollständig gesendet werden.");
    }
    private static bool IsExtended(int key) => key is >= 0x21 and <= 0x2e or 0x6f;
}
