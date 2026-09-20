using Fish.Core;
using Fish.Native;
using Fish.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Fish;

public sealed partial class MainWindow
{
    private readonly UISettings _uiSettings = new();
    private bool _introductionOpen, _adjustingWindow;
    private bool MotionReduced => _options.ReduceMotion || !_uiSettings.AnimationsEnabled;

    private void InitializeExperience()
    {
        ThemeOption.SelectedIndex = _options.Theme switch { "Dark" => 1, "Light" => 2, _ => 0 };
        ThemeOption.SelectionChanged += (_, _) =>
        {
            _options.Theme = ThemeOption.SelectedIndex switch { 1 => "Dark", 2 => "Light", _ => "System" };
            ApplyThemeChoice(); ScheduleSave();
        };
        BindOption(ReduceMotionOption, _options.ReduceMotion, value => { _options.ReduceMotion = value; ApplyMotion(); });
        BindOption(AlwaysOnTopOption, _options.AlwaysOnTop, value => { _options.AlwaysOnTop = value; ApplyTopmost(); });
        Root.ActualThemeChanged += (_, _) => RefreshAppearance();
        ApplyThemeChoice(); ApplyMotion();
        Shortcut(VirtualKey.F, VirtualKeyModifiers.Control, () => { SectionNavigation.SelectedIndex = 0; LogSearch.Focus(FocusState.Keyboard); });
        var clearSearch = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = VirtualKey.Escape };
        clearSearch.Invoked += (_, args) => { LogSearch.Text = ""; args.Handled = true; };
        LogSearch.KeyboardAccelerators.Add(clearSearch);
        Shortcut(VirtualKey.F1, VirtualKeyModifiers.None, () => ShowIntroduction_Click(this, new RoutedEventArgs()));
        Root.Loaded += (_, _) =>
        {
            ApplyMotion(); RefreshAppearance();
            if (!_options.WelcomeCompleted) ShowIntroduction_Click(this, new RoutedEventArgs());
        };
    }
    private void ApplyThemeChoice()
    {
        Root.RequestedTheme = _options.Theme switch { "Dark" => ElementTheme.Dark, "Light" => ElementTheme.Light, _ => ElementTheme.Default };
        RefreshAppearance();
    }
    private void RefreshAppearance()
    {
        if (_closeAllowed) return;
        var colors = ThemePalette.For(Root);
        AppWindow.TitleBar.BackgroundColor = colors.Background;
        AppWindow.TitleBar.ForegroundColor = colors.Text;
        AppWindow.TitleBar.ButtonBackgroundColor = colors.Background;
        AppWindow.TitleBar.ButtonForegroundColor = colors.Text;
        AppWindow.TitleBar.InactiveBackgroundColor = colors.Background;
        AppWindow.TitleBar.InactiveForegroundColor = colors.Text;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = colors.Background;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = colors.Text;
        PerformancePanel.ApplyTheme();
    }
    private void ApplyMotion()
    {
        if (_closeAllowed) return;
        foreach (var page in new FrameworkElement[] { OverviewContent, DetectionPage, FeaturesPage })
            page.Transitions = MotionReduced ? new TransitionCollection() : new TransitionCollection { new EntranceThemeTransition() };
        bool animate = StopButton.IsEnabled && !MotionReduced;
        SessionProgress.IsActive = animate;
        SessionProgress.Visibility = animate ? Visibility.Visible : Visibility.Collapsed;
        StatusDot.Visibility = animate ? Visibility.Collapsed : Visibility.Visible;
    }
    private void LogFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (LogSearch == null || LogLevelFilter == null || LogTimeFilter == null) return;
        _viewModel.SetLogFilter(LogSearch.Text, (LogFilterLevel)Math.Max(0, LogLevelFilter.SelectedIndex),
            LogTimeFilter.SelectedIndex switch { 1 => 5, 2 => 15, 3 => 60, _ => 0 });
    }
    private void ResetLogFilter_Click(object sender, RoutedEventArgs e)
    {
        LogSearch.Text = ""; LogLevelFilter.SelectedIndex = LogTimeFilter.SelectedIndex = 0;
        LogSearch.Focus(FocusState.Keyboard);
    }
    private void RestoreWindowPlacement()
    {
        try
        {
            var saved = new PixelRect(_options.WindowXPosition, _options.WindowYPosition, _options.WindowWidth, _options.WindowHeight);
            var fitted = FitWindow(saved);
            AppWindow.Move(new PointInt32(fitted.X, fitted.Y));
            uint dpi = Desktop.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            if (_options.WindowDpi > 0 && dpi > 0)
                fitted = FitWindow(fitted with { Width = (int)Math.Round((double)_options.WindowWidth * dpi / _options.WindowDpi), Height = (int)Math.Round((double)_options.WindowHeight * dpi / _options.WindowDpi) });
            AppWindow.MoveAndResize(new RectInt32(fitted.X, fitted.Y, fitted.Width, fitted.Height));
            if (_options.WindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
        }
        catch (Exception ex) { ShowError("Fensterposition konnte nicht wiederhergestellt werden: " + ex.Message); }
    }
    private static PixelRect FitWindow(PixelRect saved)
    {
        var monitors = Desktop.Monitors();
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.First();
        return WindowPlacement.Fit(saved, monitors.Select(m => m.WorkArea ?? m.Bounds).ToArray(), primary.WorkArea ?? primary.Bounds);
    }
    private void EnsureWindowVisible()
    {
        if (_adjustingWindow || _closing || _closeAllowed || AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored }) return;
        try
        {
            _adjustingWindow = true;
            var current = new PixelRect(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
            var fitted = FitWindow(current);
            if (fitted != current) AppWindow.MoveAndResize(new RectInt32(fitted.X, fitted.Y, fitted.Width, fitted.Height));
        }
        catch (Exception ex) { ShowError("Fenster konnte nicht in den sichtbaren Bereich verschoben werden: " + ex.Message); }
        finally { _adjustingWindow = false; }
    }
    private void CenterWindow_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.Restore();
        var current = new PixelRect(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
        var monitors = Desktop.Monitors();
        var monitor = monitors.OrderByDescending(m => { var overlap = current.Intersect(m.Bounds); return (long)overlap.Width * overlap.Height; }).First();
        var area = monitor.WorkArea ?? monitor.Bounds;
        int width = Math.Min(AppWindow.Size.Width, area.Width), height = Math.Min(AppWindow.Size.Height, area.Height);
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
        ScheduleSave();
    }
    private async void ShowIntroduction_Click(object sender, RoutedEventArgs e)
    {
        if (_introductionOpen || _closing || _closeAllowed || Root.XamlRoot == null) return;
        _introductionOpen = true;
        string[] titles = ["Willkommen bei Fish", "Die Anwendung bedienen", "Du behältst die Kontrolle"];
        string[] texts = [
            "Übersicht zeigt Status, durchsuchbares Protokoll und Performance. Erkennung und Aktionen enthalten die Einstellungen. Vor dem ersten Start: WoW öffnen, WeakAura prüfen und Angeln auf Taste 1 legen.",
            "F5 startet, Umschalt+F5 stoppt. Strg+F durchsucht das Protokoll; Text, Stufe und Zeitraum lassen sich kombinieren. Kopieren exportiert die sichtbaren Treffer. Mit Tab erreichst du die Bedienelemente.",
            "Unter Anwendung wählst du System, Dark oder Light, reduzierst Bewegung und schaltest Immer im Vordergrund um. Änderungen werden automatisch gespeichert. Minimieren kann Fish in den Tray verschieben; dessen Menü bietet Öffnen, Stop und Beenden. Der globale Stop-Hotkey ist dort einstellbar. Diese Einführung erreichst du jederzeit mit F1."
        ];
        int step = 0;
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 16, MaxWidth = 460 };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme, Content = text,
            PrimaryButtonText = "Weiter", SecondaryButtonText = "Zurück", CloseButtonText = "Überspringen", DefaultButton = ContentDialogButton.Primary };
        void Update()
        {
            dialog.Title = $"{step + 1}/3 · {titles[step]}"; text.Text = texts[step];
            dialog.IsSecondaryButtonEnabled = step > 0; dialog.PrimaryButtonText = step == 2 ? "Fertig" : "Weiter";
        }
        dialog.PrimaryButtonClick += (_, args) => { if (step < 2) { args.Cancel = true; step++; Update(); } };
        dialog.SecondaryButtonClick += (_, args) => { args.Cancel = true; if (step > 0) step--; Update(); };
        Update();
        try
        {
            await dialog.ShowAsync();
            _options.WelcomeCompleted = true; PersistChanges();
        }
        catch (Exception ex) { if (!_closeAllowed) ShowError("Einführung konnte nicht angezeigt werden: " + ex.Message); }
        finally { _introductionOpen = false; }
    }

}
