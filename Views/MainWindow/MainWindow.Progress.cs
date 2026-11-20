using Fish.Core;
using Microsoft.UI.Xaml.Automation;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private StepProgress? _currentStep;
    private StepCompletion? _stepFlash;
    private DateTimeOffset _stepFlashUntil;
    private int _lastStepCompletion;
    private bool _progressIdle;

    private void UpdateStepProgress(BotUpdate update)
    {
        if (update.State == BotState.Stopped || _cancellation?.IsCancellationRequested == true)
        { ResetStepProgress(); return; }
        _currentStep = update.Progress;
        // Completion stays on subsequent updates, so dispatcher coalescing cannot swallow the flash.
        if (update.CompletedStep is { } completion && completion.Id > _lastStepCompletion)
        {
            _lastStepCompletion = completion.Id;
            _stepFlash = completion;
            _stepFlashUntil = DateTimeOffset.UtcNow.AddMilliseconds(600);
            if (completion.Success) AnimateCompletion();
        }
        RefreshStepProgress();
    }

    private void ResetStepProgress()
    {
        _currentStep = null; _stepFlash = null; _lastStepCompletion = 0;
        RefreshStepProgress();
    }

    private void RefreshStepProgress()
    {
        if (StepTimeProgress == null || _closeAllowed) return;
        var now = DateTimeOffset.UtcNow;
        if (now >= _stepFlashUntil || _currentStep?.State == BotState.ApplyingBait) _stepFlash = null;
        var progress = _stepFlash?.Progress ?? _currentStep;
        bool idle = progress == null;
        if (idle && _progressIdle) return;
        _progressIdle = idle;
        double maximum = progress?.MaximumSeconds ?? 0;
        double elapsed = progress?.ElapsedSeconds(_stepFlash?.CompletedAt ?? now) ?? 0;
        string label = progress?.State switch
        {
            BotState.ApplyingBait => "Köder anbringen", BotState.Casting => "Aufsetzen", BotState.SearchingBobber => "Suche",
            BotState.Fishing => "Warten", BotState.Looting => "Nächster Wurf", _ => "Startklar"
        };
        if (_stepFlash is { } flash)
            label = (flash.Progress.State, flash.Success) switch
            {
                (BotState.SearchingBobber, true) => "Schwimmer gefunden",
                (BotState.SearchingBobber, false) => "Kein Schwimmer gefunden",
                (BotState.Casting, false) => "Wurfzeit abgelaufen",
                (_, true) => "Biss erkannt",
                _ => "Kein Biss"
            };
        StepCaption.Text = label;
        StepTimeText.Text = $"{elapsed:F1} / {maximum:F1} s";
        StepTimeProgress.Maximum = Math.Max(.001, maximum);
        StepTimeProgress.Value = _stepFlash != null ? StepTimeProgress.Maximum : elapsed;
        StepTimeProgress.Foreground = _stepFlash is { } result
            ? result.Success ? Appearance.Running : Appearance.Stopped : Appearance.Detection;
        StepProgressPanel.Background = _stepFlash is { } flashResult
            ? flashResult.Success ? Appearance.SuccessFlash : Appearance.TimeoutFlash : Appearance.Transparent;
        AutomationProperties.SetHelpText(StepTimeProgress, $"{label} · {StepTimeText.Text}");
    }

    private void RefreshBaitProgress()
    {
        if (_closeAllowed) return;
        var (_, remaining, duration) = _baitCountdown.GetStatus();
        _settingsEditor?.UpdateBaitTimer(duration > TimeSpan.Zero, _options.BaitEnabled);
        if (!_options.BaitEnabled)
        {
            BaitTimeText.Text = "Deaktiviert";
            BaitTimeProgress.Value = 0;
            return;
        }
        var displayTime = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        BaitTimeText.Text = remaining > TimeSpan.Zero ? displayTime.ToString(@"m\:ss") : "Inaktiv";
        BaitTimeProgress.Value = remaining > TimeSpan.Zero ? remaining.TotalSeconds / duration.TotalSeconds : 0;
    }
}
