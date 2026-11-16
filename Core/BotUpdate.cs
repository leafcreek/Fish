namespace Fish.Core;

public enum BotState { Stopped, WoWNotRunning, WoWNotActive, Idle, Casting, SearchingBobber, Fishing, Looting, AdjustingArea }

public sealed record BotUpdate(BotState State, string Activity, PixelRect? BobberArea = null, PixelRect? SplashArea = null,
    nint Game = 0, PixelPoint? Bobber = null, StepProgress? Progress = null, StepCompletion? CompletedStep = null);

public static class BotStateLabels
{
    public static string Label(this BotState state) => state switch
    {
        BotState.WoWNotRunning => "WoW nicht gefunden", BotState.WoWNotActive => "WoW nicht sichtbar oder minimiert",
        BotState.Idle => "Bereit zum Auswerfen", BotState.Casting => "Angel auswerfen",
        BotState.SearchingBobber => "Schwimmer suchen", BotState.Fishing => "Spritzer beobachten",
        BotState.Looting => "Wartezeit nach Biss", BotState.AdjustingArea => "Suchbereich anpassen", _ => "Bereit"
    };
}
