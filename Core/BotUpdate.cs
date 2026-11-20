namespace Fish.Core;

public enum BotState { Stopped, WoWNotRunning, WoWNotActive, Idle, Casting, SearchingBobber, Fishing, Looting, AdjustingArea, ApplyingBait }

public sealed record BotUpdate(BotState State, string Activity, PixelRect? BobberArea = null, PixelRect? SplashArea = null,
    nint Game = 0, PixelPoint? Bobber = null, StepProgress? Progress = null, StepCompletion? CompletedStep = null);

public static class BotStateLabels
{
    public static string Label(this BotState state) => state switch
    {
        BotState.WoWNotRunning => "Warte auf WoW", BotState.WoWNotActive => "WoW ausgeblendet",
        BotState.Idle => "Nächster Wurf", BotState.ApplyingBait => "Köder anbringen", BotState.Casting => "Auswerfen",
        BotState.SearchingBobber => "Schwimmer suchen", BotState.Fishing => "Auf Biss warten",
        BotState.Looting => "Einholen", BotState.AdjustingArea => "Bereich anpassen", _ => "Bereit"
    };
}
