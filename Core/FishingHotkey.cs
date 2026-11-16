namespace Fish.Core;

[Flags]
public enum HotkeyModifiers { None = 0, Control = 1, Alt = 2, Shift = 4 }

/// <summary>A single game key with optional modifiers; independent of WinUI and keyboard layout.</summary>
public readonly record struct FishingHotkey(int Key, HotkeyModifiers Modifiers = HotkeyModifiers.None)
{
    private static readonly Dictionary<string, int> Names = BuildNames();
    public static FishingHotkey Default => new(0x31);
    public int[] PressedKeys()
    {
        var keys = new List<int>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) keys.Add(0x11);
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) keys.Add(0x12);
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) keys.Add(0x10);
        keys.Add(Key); return keys.ToArray();
    }
    public string DisplayName => ToString().Replace("Ctrl+", "Strg+").Replace("Shift+", "Umschalt+");
    public static bool TryCreate(int key, HotkeyModifiers modifiers, out FishingHotkey hotkey)
    {
        hotkey = new(key, modifiers);
        bool reserved = modifiers.HasFlag(HotkeyModifiers.Alt) && key is 0x09 or 0x20 or 0x73 ||
            modifiers.HasFlag(HotkeyModifiers.Control | HotkeyModifiers.Alt) && key == 0x2e;
        return !reserved && (modifiers & ~(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift)) == 0 && Names.ContainsValue(key);
    }
    public static bool TryParse(string? text, out FishingHotkey hotkey)
    {
        hotkey = Default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = HotkeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            var modifier = part.ToUpperInvariant() switch
            {
                "CTRL" or "STRG" => HotkeyModifiers.Control,
                "ALT" => HotkeyModifiers.Alt,
                "SHIFT" or "UMSCHALT" => HotkeyModifiers.Shift,
                _ => HotkeyModifiers.None
            };
            if (modifier == HotkeyModifiers.None || modifiers.HasFlag(modifier)) return false;
            modifiers |= modifier;
        }
        if (!Names.TryGetValue(parts[^1], out int key)) return false;
        return TryCreate(key, modifiers, out hotkey);
    }
    public override string ToString()
    {
        int code = Key;
        string key = Names.FirstOrDefault(pair => pair.Value == code).Key ?? "?";
        return (Modifiers.HasFlag(HotkeyModifiers.Control) ? "Ctrl+" : "") +
            (Modifiers.HasFlag(HotkeyModifiers.Alt) ? "Alt+" : "") +
            (Modifiers.HasFlag(HotkeyModifiers.Shift) ? "Shift+" : "") + key;
    }
    private static Dictionary<string, int> BuildNames()
    {
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int key = '0'; key <= '9'; key++) names.Add(((char)key).ToString(), key);
        for (int key = 'A'; key <= 'Z'; key++) names.Add(((char)key).ToString(), key);
        for (int i = 1; i <= 24; i++) names.Add($"F{i}", 0x6f + i);
        for (int i = 0; i <= 9; i++) names.Add($"Num{i}", 0x60 + i);
        foreach (var (name, key) in new (string, int)[] {
            ("Space", 0x20), ("Tab", 0x09), ("Enter", 0x0d), ("Backspace", 0x08),
            ("Insert", 0x2d), ("Delete", 0x2e), ("Home", 0x24), ("End", 0x23),
            ("PageUp", 0x21), ("PageDown", 0x22), ("Left", 0x25), ("Up", 0x26), ("Right", 0x27), ("Down", 0x28),
            ("NumMultiply", 0x6a), ("NumAdd", 0x6b), ("NumSubtract", 0x6d), ("NumDecimal", 0x6e), ("NumDivide", 0x6f),
            ("Oem1", 0xba), ("OemPlus", 0xbb), ("OemComma", 0xbc), ("OemMinus", 0xbd),
            ("OemPeriod", 0xbe), ("Oem2", 0xbf), ("Oem3", 0xc0), ("Oem4", 0xdb),
            ("Oem5", 0xdc), ("Oem6", 0xdd), ("Oem7", 0xde), ("Oem8", 0xdf), ("Oem102", 0xe2)
        }) names.Add(name, key);
        return names;
    }
}
