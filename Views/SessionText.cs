using Fish.Core;

namespace Fish.Views;

internal static class SessionText
{
    public static string Detail(BotUpdate update)
    {
        if (update.Activity.Contains("außerhalb")) return "Suchbereich ins WoW-Fenster ziehen.";
        if (update.Activity.StartsWith("Wurf konnte")) return "Angeltaste prüfen. Neuer Versuch.";
        if (update.Activity.Contains("fehlgeschlagen")) return "Einholen fehlgeschlagen. Neuer Versuch.";
        if (update.Activity.StartsWith("Bildwechsel")) return "Bild geändert. Suche beginnt neu.";
        if (update.Activity.StartsWith("Timeout")) return "Zeit abgelaufen. Neuer Wurf.";
        return update.State switch
        {
            BotState.WoWNotRunning => "WoW öffnen, dann geht’s weiter.",
            BotState.WoWNotActive => "WoW wieder einblenden.",
            BotState.Casting => "Der Schwimmer setzt auf.",
            BotState.SearchingBobber => update.Activity.StartsWith("Schwimmer bestätigen") ? "Fund wird geprüft." : "Im markierten Bereich.",
            BotState.Fishing => update.Activity == "Spritzer kalibrieren" ? "Wasserbewegung kennenlernen." : "Der Schwimmer bleibt im Blick.",
            BotState.Looting => "Fang einholen, dann neu auswerfen.",
            BotState.AdjustingArea => "Ziehen oder an der Ecke vergrößern.",
            BotState.Idle => "Gleich geht’s weiter.",
            _ => "Mit Starten geht’s los."
        };
    }
}
