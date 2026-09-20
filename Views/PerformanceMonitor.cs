using Fish.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fish.Views;

public sealed class PerformanceMonitor : UserControl
{
    private readonly Border _card = new();
    private readonly TextBlock _session = Text("Noch keine Sitzung", 10);
    private readonly TextBlock _loot = Text("—", 19), _casts = Text("—", 19), _success = Text("—", 19), _scan = Text("—", 19);
    private readonly TextBlock _details = Text("Starten, um Sitzungswerte zu erfassen.", 10);
    private readonly LiveChart _usage = new("CPU / RAM", "%", "MB");
    private readonly LiveChart _scans = new("Ø Scan", "ms");
    private readonly List<(DateTimeOffset At, double Cpu, double Ram, double Scan)> _history = [];

    public PerformanceMonitor()
    {
        _card.BorderThickness = new(1); _card.CornerRadius = new(12); _card.Padding = new(14);
        var root = new StackPanel { Spacing = 8 };
        var heading = new Grid();
        _session.HorizontalAlignment = HorizontalAlignment.Right;
        heading.Children.Add(_session); root.Children.Add(heading);
        var metrics = new Grid { ColumnSpacing = 12 };
        for (int i = 0; i < 4; i++) metrics.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        AddMetric(metrics, 0, "Loot / min", _loot, "WeakAura-Loot-Bestätigungen, keine tatsächliche Anzahl Fische. Letzte 60 Sekunden; anfangs auf eine Minute hochgerechnet.");
        AddMetric(metrics, 1, "Würfe / min", _casts, "Angeforderte Würfe in den letzten 60 Sekunden; anfangs auf eine Minute hochgerechnet.");
        AddMetric(metrics, 2, "Erfolgreiche Würfe", _success, "Eigene Würfe mit mindestens einer Loot-Bestätigung / alle angeforderten Würfe. Der laufende Wurf zählt bereits zum Nenner; übernommene Würfe zählen nicht als eigener Erfolg.");
        AddMetric(metrics, 3, "Ø Scan (5 s)", _scan, "Mittlere Dauer von Aufnahme, Fensterprüfung und Bobber-/Splash-Suche in den letzten 5 Sekunden. Ohne Wartezeit und WeakAura-Pixelaufnahme.");
        root.Children.Add(metrics);
        var charts = new Grid { ColumnSpacing = 16 };
        charts.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        charts.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        charts.Children.Add(_usage); Grid.SetColumn(_scans, 1); charts.Children.Add(_scans);
        root.Children.Add(charts); root.Children.Add(_details);
        _card.Child = root; Content = _card;
        Loaded += (_, _) => ApplyTheme();
        ActualThemeChanged += (_, _) => ApplyTheme();
    }

    public void ApplyTheme()
    {
        var colors = ThemePalette.For(this);
        _card.Background = new SolidColorBrush(colors.Background);
        _card.BorderBrush = new SolidColorBrush(colors.Border);
        Foreground = new SolidColorBrush(colors.Text);
        _loot.Foreground = new SolidColorBrush(AccentColors.For(this, "Loot"));
        _casts.Foreground = new SolidColorBrush(AccentColors.For(this, "INFO"));
        _success.Foreground = new SolidColorBrush(AccentColors.For(this, "Auto-Learn"));
        _scan.Foreground = new SolidColorBrush(AccentColors.For(this, "Erkennung"));
        _usage.ApplyTheme(); _scans.ApplyTheme();
    }

    public void Reset() { _history.Clear(); _usage.Clear(); _scans.Clear(); }

    public void Refresh(PerformanceSnapshot stats, DateTimeOffset now, double? cpu, double? memory)
    {
        _session.Text = stats.Duration > TimeSpan.Zero ? $"{(stats.Running ? "LIVE" : "BEENDET")} · {(int)stats.Duration.TotalHours:00}:{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}" : "Noch keine Sitzung";
        _loot.Text = stats.Duration > TimeSpan.Zero ? stats.LootsPerMinute.ToString("F1") : "—";
        _casts.Text = stats.Duration > TimeSpan.Zero ? stats.CastsPerMinute.ToString("F1") : "—";
        _success.Text = stats.SuccessPercent is { } success ? $"{success:F0} %" : "—";
        _scan.Text = stats.ScanMilliseconds is { } scan ? $"{scan:F1} ms" : "—";
        _details.Text = $"Gesamt: {stats.Loots} Loots · {stats.Casts} Würfe · {stats.Timeouts} Timeouts · {stats.NoFish} Kein Fisch angebissen · Ø Wurf → Loot: {(stats.AverageCatchSeconds is { } seconds ? $"{seconds:F1} s" : "—")}";
        // Resource history continues while the fishing session is stopped.
        {
            _history.Add((now, cpu ?? double.NaN, memory ?? double.NaN, stats.Running ? stats.ScanMilliseconds ?? double.NaN : double.NaN));
            _history.RemoveAll(p => p.At < now.AddSeconds(-60));
            // Also bound history when the system clock is adjusted backwards.
            if (_history.Count > 61) _history.RemoveAt(0);
            _usage.Update(_history.Select(p => (p.At, p.Cpu, p.Ram)).ToArray(), now);
            _scans.Update(_history.Select(p => (p.At, p.Scan, double.NaN)).ToArray(), now);
        }
    }

    private static void AddMetric(Grid grid, int column, string title, TextBlock value, string tooltip)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Text(title, 10)); panel.Children.Add(value);
        ToolTipService.SetToolTip(panel, tooltip); Grid.SetColumn(panel, column); grid.Children.Add(panel);
    }
    private static TextBlock Text(string value, double size) => new() { Text = value, FontSize = Math.Max(11, size) + 1, TextWrapping = TextWrapping.Wrap };

}
