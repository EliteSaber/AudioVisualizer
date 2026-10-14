using System.Windows;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Rendering;

/// <summary>
/// Визуализация спектра: столбики, расходящиеся от центральной линии вверх и вниз.
/// Рисование идёт целиком в <see cref="OnRender"/>, а не в дереве XAML: на кадре 1920x1080
/// столбиков больше сотни, и сотня <c>Rectangle</c> обновляла бы только себя, а не весь кадр.
/// </summary>
public class SpectrumElement : FrameworkElement
{
    public static readonly DependencyProperty SnapshotProperty = DependencyProperty.Register(
        nameof(Snapshot),
        typeof(SpectrumSnapshot),
        typeof(SpectrumElement),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public SpectrumSnapshot? Snapshot
    {
        get => (SpectrumSnapshot?)GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var frame = new Rect(0d, 0d, ActualWidth, ActualHeight);
        var snapshot = Snapshot;

        if (snapshot is null || frame.IsEmpty)
        {
            return;
        }

        drawingContext.DrawRectangle(ColorBrushes.Solid(snapshot.Colors.Background), null, frame);

        if (snapshot.BarCount == 0)
        {
            return;
        }

        var layout = new BarLayout(frame.Width, frame.Height, snapshot.Bars);
        if (layout.BarCount == 0)
        {
            return;
        }

        var pen = CreatePen(snapshot.Bars, snapshot.Colors);
        var upperFill = CreateFill(snapshot, layout, upper: true);
        var lowerFill = CreateFill(snapshot, layout, upper: false);

        // Число значений может не совпасть с числом столбиков: лишние просто не рисуются,
        // недостающие считаются нулевыми. Так снимок не зависит от того, кто его собрал.
        for (var index = 0; index < layout.BarCount; index++)
        {
            var value = index < snapshot.BarCount ? snapshot.Values[index] : 0f;

            DrawBar(drawingContext, layout.GetUpperBar(index, value), snapshot.Bars, pen, upperFill, outerCornersAtTop: true);

            if (layout.IsMirrored)
            {
                DrawBar(drawingContext, layout.GetLowerBar(index, value), snapshot.Bars, pen, lowerFill, outerCornersAtTop: false);
            }
        }

        var centerLine = layout.GetCenterLine();
        if (centerLine.Height > 0d)
        {
            drawingContext.DrawRectangle(ColorBrushes.Solid(snapshot.Colors.CenterLine), null, centerLine);
        }
    }

    private static void DrawBar(DrawingContext drawingContext, Rect rect, BarSettings settings, Pen? pen, Brush fill, bool outerCornersAtTop)
    {
        if (rect.Height <= 0d || rect.Width <= 0d)
        {
            return;
        }

        var geometry = BarGeometry.Build(rect, settings.CornerRadius, settings.CornerStyle, outerCornersAtTop);
        drawingContext.DrawGeometry(fill, pen, geometry);
    }

    private static Pen? CreatePen(BarSettings settings, ColorSettings colors)
    {
        if (settings.BorderWidth <= 0d || colors.BarBorder.A == 0)
        {
            return null;
        }

        var pen = new Pen(ColorBrushes.Solid(colors.BarBorder), settings.BorderWidth) { LineJoin = PenLineJoin.Round };
        pen.Freeze();

        return pen;
    }

    /// <summary>
    /// Заливка одной половины столбика. Градиенты строятся на весь кадр, а не на столбик:
    /// у всех столбиков одинаковые вертикальные границы, поэтому одного расчёта на кадр достаточно.
    /// </summary>
    private static Brush CreateFill(SpectrumSnapshot snapshot, BarLayout layout, bool upper)
    {
        var colors = snapshot.Colors;

        if (snapshot.Bars.FillMode is BarFillMode.Solid)
        {
            return ColorBrushes.Solid(colors.BarFill);
        }

        var center = upper ? layout.CenterY - layout.HalfGap : layout.CenterY + layout.HalfGap;
        var outer = upper
            ? layout.CenterY - layout.HalfGap - layout.MaxHalfHeight
            : layout.CenterY + layout.HalfGap + layout.MaxHalfHeight;

        return upper
            ? ColorBrushes.Vertical(colors.BarFillSecondary, colors.BarFill, new Point(0d, outer), new Point(0d, center))
            : ColorBrushes.Vertical(colors.BarFill, colors.BarFillSecondary, new Point(0d, center), new Point(0d, outer));
    }
}