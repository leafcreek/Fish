using Fish.Configuration;
using Fish.Core;

static class HotkeyTests
{
    public static void Run(Action<bool, string> check)
    {
        check(FishingHotkey.TryParse("1", out var plain) && plain == FishingHotkey.Default, "Standard-Hotkey bleibt Taste 1");
        check(FishingHotkey.TryParse(" Umschalt + strg + f8 ", out var combo) && combo.ToString() == "Ctrl+Shift+F8" &&
            combo.DisplayName == "Strg+Umschalt+F8" && combo.PressedKeys().SequenceEqual([0x11, 0x10, 0x77]),
            "Kombinationen normalisieren deutsche Bezeichnungen und besitzen eine feste Reihenfolge");
        foreach (string key in new[] { "F24", "Num7", "Alt+Q", "Ctrl+Alt+Shift+Q", "OemMinus", "Shift+NumAdd" })
            check(FishingHotkey.TryParse(key, out var parsed) && FishingHotkey.TryParse(parsed.ToString(), out var reloaded) && reloaded == parsed,
                $"Hotkey lässt sich verlustfrei speichern: {key}");
        foreach (string key in new[] { "", "Ctrl+", "Ctrl+Ctrl+1", "Win+1", "Escape", "F25", "Shift+1+2", "???", "Alt+F4", "Alt+Tab", "Ctrl+Alt+Delete" })
            check(!FishingHotkey.TryParse(key, out _), $"Ungültige Hotkey-Belegung wird abgewiesen: '{key}'");
        check(!FishingHotkey.TryCreate(0x11, HotkeyModifiers.None, out _) &&
            !FishingHotkey.TryCreate('A', (HotkeyModifiers)8, out _), "Reine Modifier und unbekannte Flags können nicht aufgenommen werden");
        var invalid = new BotOptions { FishingHotkey = "invalid" }; invalid.Normalize();
        check(invalid.FishingHotkey == "1", "Ungültiger gespeicherter Hotkey erhält einen benutzbaren Standard");
        var source = new BotOptions { FishingHotkey = "Ctrl+F8" };
        var snapshot = FishingOptions.From(source); source.FishingHotkey = "Q";
        check(snapshot.Hotkey.ToString() == "Ctrl+F8", "Laufende Konfiguration ist vom veränderlichen UI-Modell unabhängig");
    }
}
