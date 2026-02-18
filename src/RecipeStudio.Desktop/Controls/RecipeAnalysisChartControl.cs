using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Controls;

public enum AnalysisChartType
{
    Speeds,
    Angles,
    Coordinates,
    Acceleration
}

public sealed class RecipeAnalysisChartControl : Control
{
    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<RecipeAnalysisChartControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<AnalysisChartType> ChartTypeProperty =
        AvaloniaProperty.Register<RecipeAnalysisChartControl, AnalysisChartType>(nameof(ChartType));

    private INotifyCollectionChanged? _collectionChanged;
    private readonly Dictionary<RecipePoint, PropertyChangedEventHandler> _pointHandlers = new();

    private readonly Thickness _padding = new(52, 20, 74, 48);
    private int _hoverIndex = -1;

    public IList<RecipePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public AnalysisChartType ChartType
    {
        get => GetValue(ChartTypeProperty);
        set => SetValue(ChartTypeProperty, value);
    }

    static RecipeAnalysisChartControl()
    {
        PointsProperty.Changed.AddClassHandler<RecipeAnalysisChartControl>((c, e) => c.OnPointsChanged((IList<RecipePoint>?)e.NewValue));
        ChartTypeProperty.Changed.AddClassHandler<RecipeAnalysisChartControl>((c, _) => c.InvalidateVisual());
    }

    private void OnPointsChanged(IList<RecipePoint>? points)
    {
        if (_collectionChanged is not null)
        {
            _collectionChanged.CollectionChanged -= OnCollectionChanged;
            _collectionChanged = null;
        }

        foreach (var (p, h) in _pointHandlers.ToArray())
        {
            p.PropertyChanged -= h;
            _pointHandlers.Remove(p);
        }

        if (points is INotifyCollectionChanged incc)
        {
            _collectionChanged = incc;
            incc.CollectionChanged += OnCollectionChanged;
        }

        if (points is not null)
        {
            foreach (var p in points)
            {
                PropertyChangedEventHandler handler = (_, __) => InvalidateVisual();
                p.PropertyChanged += handler;
                _pointHandlers[p] = handler;
            }
        }

        InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<RecipePoint>())
            {
                if (_pointHandlers.TryGetValue(item, out var h))
                {
                    item.PropertyChanged -= h;
                    _pointHandlers.Remove(item);
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var p in e.NewItems.OfType<RecipePoint>())
            {
                PropertyChangedEventHandler handler = (_, __) => InvalidateVisual();
                p.PropertyChanged += handler;
                _pointHandlers[p] = handler;
            }
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(new SolidColorBrush(Color.FromRgb(8, 23, 53)), Bounds);

        var points = Points;
        if (points is null || points.Count == 0)
        {
            DrawText(context, "Нет данных", new Point(Bounds.Width / 2, Bounds.Height / 2), 15, Brushes.White, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var chartRect = new Rect(
            _padding.Left,
            _padding.Top,
            Math.Max(1, Bounds.Width - _padding.Left - _padding.Right),
            Math.Max(1, Bounds.Height - _padding.Top - _padding.Bottom));

        var series = BuildSeries(points);
        var xValues = points.Select((p, idx) => p.NPoint > 0 ? p.NPoint : idx + 1).ToArray();
        var minX = xValues.Min();
        var maxX = xValues.Max();
        if (maxX == minX) maxX += 1;

        var leftSeries = series.Where(s => !s.RightAxis).ToArray();
        var rightSeries = series.Where(s => s.RightAxis).ToArray();

        var yLeft = ResolveRange(leftSeries.SelectMany(s => s.Values));
        var yRight = rightSeries.Length > 0 ? ResolveRange(rightSeries.SelectMany(s => s.Values)) : yLeft;

        DrawGrid(context, chartRect, minX, maxX, yLeft.min, yLeft.max);
        if (rightSeries.Length > 0)
        {
            DrawRightAxis(context, chartRect, yRight.min, yRight.max, series.First(s => s.RightAxis).AxisTitle);
        }

        foreach (var s in series)
        {
            var yRange = s.RightAxis ? yRight : yLeft;
            DrawSeries(context, chartRect, xValues, s, minX, maxX, yRange.min, yRange.max);
        }

        DrawLeftAxisLabel(context, chartRect, leftSeries.FirstOrDefault().AxisTitle ?? "");
        DrawLegend(context, chartRect, series);

        if (_hoverIndex >= 0 && _hoverIndex < points.Count)
        {
            DrawHover(context, chartRect, points, series, xValues, minX, maxX, yLeft, yRight);
        }
    }

    private static (double min, double max) ResolveRange(IEnumerable<double> values)
    {
        var data = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
        if (data.Length == 0)
            return (0, 1);

        var min = data.Min();
        var max = data.Max();

        if (Math.Abs(max - min) < 0.0001)
        {
            var d = Math.Max(1, Math.Abs(min * 0.2));
            return (min - d, max + d);
        }

        var padding = (max - min) * 0.1;
        return (min - padding, max + padding);
    }

    private static void DrawGrid(DrawingContext ctx, Rect chartRect, int minX, int maxX, double minY, double maxY)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(70, 109, 128, 160)), 1, dashStyle: new DashStyle(new[] { 2.5, 4.0 }, 0));
        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(95, 114, 145)), 1.2);

        const int hLines = 5;
        for (var i = 0; i <= hLines; i++)
        {
            var y = chartRect.Top + chartRect.Height * i / hLines;
            ctx.DrawLine(i == hLines ? axisPen : gridPen, new Point(chartRect.Left, y), new Point(chartRect.Right, y));

            var value = maxY - (maxY - minY) * i / hLines;
            DrawText(ctx, $"{value:0.#}", new Point(chartRect.Left - 8, y), 13, new SolidColorBrush(Color.FromRgb(105, 175, 255)), HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        var xTickCount = Math.Min(4, maxX - minX + 1);
        for (var i = 0; i < xTickCount; i++)
        {
            var t = xTickCount == 1 ? 0 : i / (double)(xTickCount - 1);
            var x = chartRect.Left + chartRect.Width * t;
            ctx.DrawLine(gridPen, new Point(x, chartRect.Top), new Point(x, chartRect.Bottom));

            var value = minX + (maxX - minX) * t;
            DrawText(ctx, $"{value:0}", new Point(x, chartRect.Bottom + 18), 14, new SolidColorBrush(Color.FromRgb(186, 201, 224)), HorizontalAlignment.Center);
        }

        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(86, 104, 136)), 1), chartRect);
    }

    private static void DrawRightAxis(DrawingContext ctx, Rect chartRect, double minY, double maxY, string title)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(255, 102, 112)), 1.3);
        ctx.DrawLine(pen, new Point(chartRect.Right, chartRect.Top), new Point(chartRect.Right, chartRect.Bottom));

        const int hLines = 4;
        for (var i = 0; i <= hLines; i++)
        {
            var y = chartRect.Top + chartRect.Height * i / hLines;
            var value = maxY - (maxY - minY) * i / hLines;
            DrawText(ctx, $"{value:0.#}", new Point(chartRect.Right + 6, y), 13, new SolidColorBrush(Color.FromRgb(255, 102, 112)), HorizontalAlignment.Left, VerticalAlignment.Center);
        }

        DrawText(ctx, title, new Point(chartRect.Right + 42, chartRect.Top + chartRect.Height / 2), 14, new SolidColorBrush(Color.FromRgb(166, 174, 190)), VerticalAlignment.Center);
    }

    private static void DrawSeries(DrawingContext ctx, Rect chartRect, int[] xValues, SeriesInfo series, int minX, int maxX, double minY, double maxY)
    {
        if (series.Values.Count == 0) return;

        var pen = new Pen(series.Color, 2, dashStyle: series.Dashed ? new DashStyle(new[] { 3.0, 3.0 }, 0) : null);

        StreamGeometry? geometry = null;
        using (var g = new StreamGeometry())
        {
            using var sg = g.Open();
            var start = ToScreen(chartRect, xValues[0], series.Values[0], minX, maxX, minY, maxY);
            sg.BeginFigure(start, false);
            for (var i = 1; i < series.Values.Count; i++)
            {
                sg.LineTo(ToScreen(chartRect, xValues[i], series.Values[i], minX, maxX, minY, maxY));
            }
            sg.EndFigure(false);
            geometry = g;
        }

        if (geometry is not null)
        {
            ctx.DrawGeometry(null, pen, geometry);
        }

        for (var i = 0; i < series.Values.Count; i++)
        {
            var p = ToScreen(chartRect, xValues[i], series.Values[i], minX, maxX, minY, maxY);
            ctx.DrawEllipse(series.Color, new Pen(Brushes.White, 1.2), p, 4, 4);
        }
    }

    private static void DrawLeftAxisLabel(DrawingContext ctx, Rect chartRect, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        DrawText(ctx, text, new Point(chartRect.Left - 46, chartRect.Top + chartRect.Height / 2), 16, new SolidColorBrush(Color.FromRgb(161, 171, 190)), HorizontalAlignment.Right, VerticalAlignment.Center);
    }

    private static void DrawLegend(DrawingContext ctx, Rect chartRect, IReadOnlyList<SeriesInfo> series)
    {
        var y = chartRect.Bottom + 28;
        var totalWidth = series.Sum(s => 22 + MeasureText(s.Name, 13).Width) + (series.Count - 1) * 18;
        var x = chartRect.Left + (chartRect.Width - totalWidth) / 2;

        foreach (var s in series)
        {
            ctx.DrawLine(new Pen(s.Color, 2), new Point(x, y), new Point(x + 14, y));
            ctx.DrawEllipse(s.Color, new Pen(Brushes.White, 1), new Point(x + 7, y), 2.5, 2.5);
            x += 18;
            DrawText(ctx, s.Name, new Point(x, y - 7), 13, s.Color);
            x += MeasureText(s.Name, 13).Width + 22;
        }
    }

    private void DrawHover(DrawingContext ctx, Rect chartRect, IList<RecipePoint> points, IReadOnlyList<SeriesInfo> series, int[] xValues, int minX, int maxX, (double min, double max) yLeft, (double min, double max) yRight)
    {
        var idx = Math.Clamp(_hoverIndex, 0, points.Count - 1);
        var x = chartRect.Left + chartRect.Width * (xValues[idx] - minX) / Math.Max(1, maxX - minX);

        ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(140, 224, 231, 255)), 1), new Point(x, chartRect.Top), new Point(x, chartRect.Bottom));

        foreach (var s in series)
        {
            var range = s.RightAxis ? yRight : yLeft;
            var p = ToScreen(chartRect, xValues[idx], s.Values[idx], minX, maxX, range.min, range.max);
            ctx.DrawEllipse(s.Color, new Pen(Brushes.White, 2), p, 4.5, 4.5);
        }

        var box = new Rect(x + 10, chartRect.Top + chartRect.Height * 0.3, 170, 34 + series.Count * 28);
        if (box.Right > chartRect.Right)
        {
            box = box.WithX(x - box.Width - 10);
        }

        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 31, 46, 70)), box);
        ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(69, 85, 112)), 1), box);

        var rowY = box.Top + 12;
        DrawText(ctx, $"{xValues[idx]}", new Point(box.Left + 10, rowY), 14, Brushes.White);
        rowY += 26;
        foreach (var s in series)
        {
            DrawText(ctx, $"{s.Name} : {s.Values[idx]:0.##}", new Point(box.Left + 10, rowY), 13, s.Color);
            rowY += 26;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var points = Points;
        if (points is null || points.Count == 0)
            return;

        var chartRect = new Rect(
            _padding.Left,
            _padding.Top,
            Math.Max(1, Bounds.Width - _padding.Left - _padding.Right),
            Math.Max(1, Bounds.Height - _padding.Top - _padding.Bottom));

        var pos = e.GetPosition(this);
        if (!chartRect.Contains(pos))
        {
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                InvalidateVisual();
            }
            return;
        }

        var xValues = points.Select((p, idx) => p.NPoint > 0 ? p.NPoint : idx + 1).ToArray();
        var minX = xValues.Min();
        var maxX = xValues.Max();
        if (maxX == minX) maxX += 1;

        var t = (pos.X - chartRect.Left) / Math.Max(1, chartRect.Width);
        var xValue = minX + t * (maxX - minX);

        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < xValues.Length; i++)
        {
            var d = Math.Abs(xValues[i] - xValue);
            if (d < bestDistance)
            {
                bestDistance = d;
                bestIndex = i;
            }
        }

        if (_hoverIndex != bestIndex)
        {
            _hoverIndex = bestIndex;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            InvalidateVisual();
        }
    }

    private List<SeriesInfo> BuildSeries(IList<RecipePoint> points)
    {
        switch (ChartType)
        {
            case AnalysisChartType.Angles:
                return new List<SeriesInfo>
                {
                    new("Alpha", points.Select(p => p.Alfa).ToList(), new SolidColorBrush(Color.FromRgb(180, 129, 255)), "Degrees"),
                    new("Beta", points.Select(p => p.Betta).ToList(), new SolidColorBrush(Color.FromRgb(64, 232, 174)), "Degrees")
                };

            case AnalysisChartType.Coordinates:
                return new List<SeriesInfo>
                {
                    new("Height (Z)", points.Select(p => p.ZCrd).ToList(), new SolidColorBrush(Color.FromRgb(89, 166, 255)), "mm"),
                    new("Machine X", points.Select(p => p.Xr0 + p.DX).ToList(), new SolidColorBrush(Color.FromRgb(255, 133, 133)), "mm", dashed: true),
                    new("Machine Z", points.Select(p => p.Zr0 + p.DZ).ToList(), new SolidColorBrush(Color.FromRgb(255, 201, 61)), "mm", dashed: true),
                    new("Radius (R)", points.Select(p => p.RCrd).ToList(), new SolidColorBrush(Color.FromRgb(60, 232, 166)), "mm")
                };

            case AnalysisChartType.Acceleration:
                var acc = new List<double>(points.Count);
                for (var i = 0; i < points.Count; i++)
                {
                    if (i == 0)
                    {
                        acc.Add(0);
                        continue;
                    }

                    var dv = Math.Abs(points[i].NozzleSpeedMmMin - points[i - 1].NozzleSpeedMmMin) / 60.0;
                    var dt = Math.Max(0.001, points[i].TimeSec);
                    acc.Add(dv / dt);
                }

                return new List<SeriesInfo>
                {
                    new("Acceleration", acc, new SolidColorBrush(Color.FromRgb(255, 128, 128)), "Accel (mm/s²)")
                };

            default:
                return new List<SeriesInfo>
                {
                    new("Flow Rate", points.Select(p => p.IceRate).ToList(), new SolidColorBrush(Color.FromRgb(255, 100, 100)), "V (mm/s)", rightAxis: true),
                    new("V (Sim)", points.Select(p => p.NozzleSpeedMmMin / 60.0).ToList(), new SolidColorBrush(Color.FromRgb(86, 164, 255)), "V (mm/s)"),
                    new("V (Table)", points.Select(p => p.SpeedTable).ToList(), new SolidColorBrush(Color.FromRgb(154, 163, 176)), "V (mm/s)")
                };
        }
    }

    private static Point ToScreen(Rect chartRect, double x, double y, int minX, int maxX, double minY, double maxY)
    {
        var tx = (x - minX) / Math.Max(1e-6, maxX - minX);
        var ty = (y - minY) / Math.Max(1e-6, maxY - minY);

        var sx = chartRect.Left + chartRect.Width * tx;
        var sy = chartRect.Bottom - chartRect.Height * ty;
        return new Point(sx, sy);
    }

    private static FormattedText MeasureText(string text, double size)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), size, Brushes.White);

    private static void DrawText(DrawingContext ctx, string text, Point p, double size, IBrush brush,
        HorizontalAlignment horizontal = HorizontalAlignment.Left,
        VerticalAlignment vertical = VerticalAlignment.Top)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), size, brush);

        var drawPoint = p;
        if (horizontal == HorizontalAlignment.Center) drawPoint = drawPoint.WithX(p.X - ft.Width / 2);
        else if (horizontal == HorizontalAlignment.Right) drawPoint = drawPoint.WithX(p.X - ft.Width);

        if (vertical == VerticalAlignment.Center) drawPoint = drawPoint.WithY(p.Y - ft.Height / 2);
        else if (vertical == VerticalAlignment.Bottom) drawPoint = drawPoint.WithY(p.Y - ft.Height);

        ctx.DrawText(ft, drawPoint);
    }

    private sealed record SeriesInfo(string Name, List<double> Values, IBrush Color, string AxisTitle, bool RightAxis = false, bool Dashed = false);
}
