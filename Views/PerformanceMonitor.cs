using Fish.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Fish.Views;

public sealed class PerformanceMonitor : UserControl
{
    private readonly Border _card = new();
    private readonly TextBlock _session = Text("Noch keine Sitzung", 10);
    private readonly TextBlock _loot = Text("—", 19), _casts = Text("—", 19), _success = Text("—", 19), _scan = Text("—", 19);
    private readonly TextBlock _details = Text("Starten, um Sitzungswerte zu erfassen.", 10);
    private readonly TextBlock _resources = Text("", 10);
    private readonly Sparkline _rates = new("Loot/min · Würfe/min", true);
    private readonly Sparkline _scans = new("Ø Scan · ms", false);
    private readonly List<(DateTimeOffset At, double Loot, double Cast, double Scan)> _history = [];

    public PerformanceMonitor()
    {
        _card.BorderThickness = new(1); _card.CornerRadius = new(12); _card.Padding = new(14);
        var root = new StackPanel { Spacing = 8 };
        var heading = new Grid();
        heading.Children.Add(new TextBlock { Text = "Performance", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
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
        charts.Children.Add(_rates); Grid.SetColumn(_scans, 1); charts.Children.Add(_scans);
        root.Children.Add(charts); root.Children.Add(_details); root.Children.Add(_resources);
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
        _rates.ApplyTheme(); _scans.ApplyTheme();
    }

    public void Reset() { _history.Clear(); _rates.Clear(); _scans.Clear(); }

    public void Refresh(PerformanceSnapshot stats, DateTimeOffset now, double? cpu, double? memory)
    {
        _session.Text = stats.Duration > TimeSpan.Zero ? $"{(stats.Running ? "LIVE" : "BEENDET")} · {(int)stats.Duration.TotalHours:00}:{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}" : "Noch keine Sitzung";
        _loot.Text = stats.Duration > TimeSpan.Zero ? stats.LootsPerMinute.ToString("F1") : "—";
        _casts.Text = stats.Duration > TimeSpan.Zero ? stats.CastsPerMinute.ToString("F1") : "—";
        _success.Text = stats.SuccessPercent is { } success ? $"{success:F0} %" : "—";
        _scan.Text = stats.ScanMilliseconds is { } scan ? $"{scan:F1} ms" : "—";
        _details.Text = $"Gesamt: {stats.Loots} Loots · {stats.Casts} Würfe · {stats.Timeouts} Timeouts · Ø Wurf → Loot: {(stats.AverageCatchSeconds is { } seconds ? $"{seconds:F1} s" : "—")}";
        _resources.Text = $"Fish: CPU {(cpu is { } percent ? $"{percent:F1} %" : "—")} · RAM {(memory is { } mb ? $"{mb:F0} MB" : "—")} · {(stats.Running && stats.Duration.TotalSeconds < 60 ? "Raten anfangs hochgerechnet" : "Raten: letzte 60 s")}";
        ToolTipService.SetToolTip(_resources, "Nur der Fish-Prozess. CPU-Anteil an allen logischen Prozessoren; RAM = Working Set. Aktualisierung jede Sekunde.");
        foreach (var value in new[] { _loot, _casts, _success, _scan })
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(value, $"{value.Tag}: {value.Text}");
        if (stats.Running)
        {
            _history.Add((now, stats.LootsPerMinute, stats.CastsPerMinute, stats.ScanMilliseconds ?? double.NaN));
            _history.RemoveAll(p => p.At < now.AddSeconds(-60));
            // Also bound history when the system clock is adjusted backwards.
            if (_history.Count > 61) _history.RemoveAt(0);
            _rates.Update(_history.Select(p => (p.At, p.Loot, p.Cast)).ToArray(), now);
            _scans.Update(_history.Select(p => (p.At, p.Scan, double.NaN)).ToArray(), now);
        }
    }

    private static void AddMetric(Grid grid, int column, string title, TextBlock value, string tooltip)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Text(title, 10)); panel.Children.Add(value);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(value, tooltip);
        value.Tag = title;
        ToolTipService.SetToolTip(panel, tooltip); Grid.SetColumn(panel, column); grid.Children.Add(panel);
    }
    private static TextBlock Text(string value, double size) => new() { Text = value, FontSize = Math.Max(11, size), TextWrapping = TextWrapping.Wrap };

    private sealed class Sparkline : StackPanel
    {
        private readonly Canvas _canvas = new() { Height = 40, Background = null };
        private readonly TextBlock _scale = Text("0", 9);
        private readonly bool _second;
        private (DateTimeOffset At, double First, double Second)[] _data = [];
        private DateTimeOffset _now;
        public Sparkline(string title, bool second)
        {
            _second = second; Spacing = 3;
            var label = Text(title + (second ? " · Linie / gestrichelt" : ""), 10);
            Children.Add(label); Children.Add(_canvas);
            var axis = new Grid();
            axis.Children.Add(Text("−60 s", 9));
            _scale.HorizontalAlignment = HorizontalAlignment.Center; axis.Children.Add(_scale);
            var right = Text("jetzt", 9); right.HorizontalAlignment = HorizontalAlignment.Right; axis.Children.Add(right);
            Children.Add(axis);
            _canvas.SizeChanged += (_, _) => Draw();
        }
        public void ApplyTheme()
        {
            _canvas.Background = new SolidColorBrush(ThemePalette.For(this).Background);
            Draw();
        }
        public void Clear() { _data = []; Draw(); }
        public void Update((DateTimeOffset At, double First, double Second)[] data, DateTimeOffset now) { _data = data; _now = now; Draw(); }
        private void Draw()
        {
            _canvas.Children.Clear();
            double maximum = Math.Max(1, Math.Ceiling(_data.SelectMany(p => new[] { p.First, p.Second }).Where(double.IsFinite).DefaultIfEmpty(0).Max()));
            _scale.Text = $"0–{maximum:F0}";
            double width = _canvas.ActualWidth;
            if (width <= 0) return;
            var colors = ThemePalette.For(this);
            Plot(false, new SolidColorBrush(colors.First));
            if (_second) Plot(true, new SolidColorBrush(colors.Second));
            void Plot(bool second, SolidColorBrush color)
            {
                Polyline? line = null;
                foreach (var point in _data)
                {
                    double value = second ? point.Second : point.First;
                    if (!double.IsFinite(value)) { line = null; continue; }
                    if (line == null)
                    {
                        line = new Polyline { Stroke = color, StrokeThickness = 1.5, Points = new PointCollection() };
                        if (second) line.StrokeDashArray = new DoubleCollection { 3, 2 };
                        _canvas.Children.Add(line);
                    }
                    double x = Math.Clamp(1 - (_now - point.At).TotalSeconds / 60, 0, 1) * Math.Max(0, width - 2) + 1;
                    double y = 38 - Math.Clamp(value / maximum, 0, 1) * 36;
                    line.Points.Add(new Point(x, y));
                }
            }
        }
    }
}
