using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Fish.Views;

public sealed class LiveChart : StackPanel
{
    private readonly Canvas _plot = new() { Height = 96, Margin = new(0, 8, 0, 8) };
    private readonly TextBlock[] _left = new TextBlock[3], _right = new TextBlock[3];
    private readonly string _title, _unit;
    private readonly string? _rightUnit;
    private (DateTimeOffset At, double First, double Second)[] _data = [];
    private DateTimeOffset _now;

    public LiveChart(string title, string unit, string? rightUnit = null)
    {
        _title = title; _unit = unit; _rightUnit = rightUnit; Spacing = 4;
        Children.Add(Label(title));
        var chart = new Grid { ColumnSpacing = 6 };
        chart.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        chart.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        chart.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        chart.RowDefinitions.Add(new() { Height = GridLength.Auto });
        chart.RowDefinitions.Add(new() { Height = GridLength.Auto });
        chart.Children.Add(Axis(_left));
        Grid.SetColumn(_plot, 1); chart.Children.Add(_plot);
        if (rightUnit != null) { var axis = Axis(_right); Grid.SetColumn(axis, 2); chart.Children.Add(axis); }
        var time = new Grid(); time.Children.Add(Label("−60 s"));
        var now = Label("jetzt"); now.HorizontalAlignment = HorizontalAlignment.Right; time.Children.Add(now);
        Grid.SetRow(time, 1); Grid.SetColumn(time, 1); chart.Children.Add(time);
        Children.Add(chart);
        _plot.SizeChanged += (_, _) => Draw();
        Loaded += (_, _) => ApplyTheme();
    }

    private static TextBlock Label(string text) => new() { Text = text, FontSize = 12 };
    private static Grid Axis(TextBlock[] labels)
    {
        var grid = new Grid();
        for (int i = 0; i < 3; i++)
        {
            labels[i] = Label("");
            labels[i].VerticalAlignment = i == 0 ? VerticalAlignment.Top : i == 1 ? VerticalAlignment.Center : VerticalAlignment.Bottom;
            grid.Children.Add(labels[i]);
        }
        return grid;
    }

    public void ApplyTheme() => Draw();
    public void Clear() { _data = []; Draw(); }
    public void Update((DateTimeOffset At, double First, double Second)[] data, DateTimeOffset now)
    {
        _data = data; _now = now; Draw();
        var last = data.LastOrDefault();
        string reading = $"{_title}: {Value(last.First)} {_unit}" + (_rightUnit == null ? "" : $" / {Value(last.Second)} {_rightUnit}");
        ToolTipService.SetToolTip(this, reading);
    }
    private static string Value(double value) => double.IsFinite(value) ? value.ToString("F1") : "—";
    private static double Maximum(IEnumerable<double> values)
    {
        double max = Math.Max(1, values.Where(double.IsFinite).DefaultIfEmpty(1).Max());
        double step = Math.Pow(10, Math.Floor(Math.Log10(max))) / 2;
        return Math.Ceiling(max / step) * step;
    }
    private void Draw()
    {
        var palette = ThemePalette.For(this);
        var first = new SolidColorBrush(palette.First); var second = new SolidColorBrush(palette.Second);
        double leftMax = _unit == "%" ? 100 : Maximum(_data.Select(p => p.First));
        double rightMax = Maximum(_data.Select(p => p.Second));
        SetAxis(_left, leftMax, _unit, first);
        if (_rightUnit != null) SetAxis(_right, rightMax, _rightUnit, second);
        _plot.Children.Clear();
        double width = _plot.ActualWidth, height = _plot.Height;
        if (width <= 0) return;
        var border = new SolidColorBrush(palette.Border);
        for (int i = 0; i < 3; i++)
            _plot.Children.Add(new Line { X1 = 0, X2 = width, Y1 = i * height / 2, Y2 = i * height / 2, Stroke = border, StrokeThickness = .5 });
        Plot(false, leftMax, first);
        if (_rightUnit != null) Plot(true, rightMax, second);
        void Plot(bool right, double maximum, SolidColorBrush brush)
        {
            Polyline? line = null;
            foreach (var sample in _data)
            {
                double value = right ? sample.Second : sample.First;
                if (!double.IsFinite(value)) { line = null; continue; }
                if (line == null)
                {
                    line = new Polyline { Stroke = brush, StrokeThickness = 2, Points = new PointCollection() };
                    if (right) line.StrokeDashArray = new DoubleCollection { 3, 2 };
                    _plot.Children.Add(line);
                }
                line.Points.Add(new Point(Math.Clamp(1 - (_now - sample.At).TotalSeconds / 60, 0, 1) * width,
                    (1 - Math.Clamp(value / maximum, 0, 1)) * height));
            }
        }
    }
    private static void SetAxis(TextBlock[] labels, double max, string unit, Brush brush)
    {
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].Text = $"{max * (1 - i / 2.0):0.#} {unit}";
            labels[i].Foreground = brush;
        }
    }
}
