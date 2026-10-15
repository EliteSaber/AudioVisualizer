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
    private readonly BarTrail _trail = new();

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
            // След забывается вместе с кадром: иначе на следующем кадре мелькнул бы призрак
            // того, что было на экране до остановки или смены файла.
            _trail.Reset();

            return;
        }

        drawingContext.DrawRectangle(ColorBrushes.Solid(snapshot.Colors.Background), null, frame);

        _trail.Advance(snapshot, frame.Width, frame.Height);

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
            var ghost = _trail.GhostOf(index, value);

            DrawTrail(drawingContext, layout, index, value, ghost, snapshot, upper: true);
            DrawBar(drawingContext, layout.GetUpperBar(index, value), snapshot.Bars, pen, upperFill, outerCornersAtTop: true);

            if (layout.IsMirrored)
            {
                DrawTrail(drawingContext, layout, index, value, ghost, snapshot, upper: false);
                DrawBar(drawingContext, layout.GetLowerBar(index, value), snapshot.Bars, pen, lowerFill, outerCornersAtTop: false);
            }
        }

        var centerLine = layout.GetCenterLine();
        if (centerLine.Height > 0d)
        {
            drawingContext.DrawRectangle(ColorBrushes.Solid(snapshot.Colors.CenterLine), null, centerLine);
        }
    }

    /// <summary>
    /// Призрак прежней высоты столбика: полоса от вершины, где столбик был, до его нынешней.
    /// У края полосы берётся цвет заливки столбика на этой высоте, поэтому стыка не видно.
    /// </summary>
    /// <remarks>
    /// Прямоугольник без скругления и без границы: края у полосы уходят в прозрачность,
    /// и скруглять или обводить там нечего. Поверх полосы всё равно рисуется сам столбик.
    /// </remarks>
    private static void DrawTrail(
        DrawingContext drawingContext,
        BarLayout layout,
        int index,
        float value,
        float ghost,
        SpectrumSnapshot snapshot,
        bool upper)
    {
        var current = upper ? layout.GetUpperBar(index, value) : layout.GetLowerBar(index, value);
        var past = upper ? layout.GetUpperBar(index, ghost) : layout.GetLowerBar(index, ghost);

        var band = upper
            ? new Rect(current.X, past.Y, current.Width, current.Y - past.Y)
            : new Rect(current.X, current.Bottom, current.Width, past.Bottom - current.Bottom);

        if (band.Width <= 0d || band.Height < BarTrail.MinimumVisibleHeight)
        {
            return;
        }

        var span = FillSpan(snapshot, layout, upper);
        var seam = upper ? current.Y : current.Bottom;

        drawingContext.DrawGeometry(
            ColorBrushes.Fade(ColorBrushes.At(span.From, span.To, span.StartY, span.EndY, seam), transparentAtEnd: !upper),
            null,
            BarGeometry.Rect(band));
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
        var span = FillSpan(snapshot, layout, upper);

        return span.StartY == span.EndY
            ? ColorBrushes.Solid(span.From)
            : ColorBrushes.Vertical(span.From, span.To, new Point(0d, span.StartY), new Point(0d, span.EndY));
    }

    /// <summary>
    /// Откуда и докуда идёт заливка половины столбика. Одно описание на обе половины: и кисть,
    /// и цвет стыка со следом читаются из него, поэтому цвет столбика и цвет следа у его края
    /// расходиться не могут.
    /// </summary>
    /// <remarks>
    /// <see cref="FillGradient.From"/> - цвет в начале градиента, то есть в
    /// <see cref="FillGradient.StartY"/>, и кисть строится именно в этом порядке. Перестановка
    /// двух цветов тихо меняет оформление на противоположное: цвет центра уезжает к краю, а
    /// цвет края - к центру.
    /// </remarks>
    private static FillGradient FillSpan(SpectrumSnapshot snapshot, BarLayout layout, bool upper)
    {
        var colors = snapshot.Colors;

        // При однотонной заливке градиента нет: у неё нулевые границы, и тогда и кисть, и
        // цвет стыка со следом берут один цвет. Иначе след на нижней половине уехал бы в
        // цвет градиента, которого на экране нет.
        if (snapshot.Bars.FillMode is BarFillMode.Solid)
        {
            return new FillGradient(colors.BarFill, colors.BarFill, 0d, 0d);
        }

        var inner = upper ? layout.CenterY - layout.HalfGap : layout.CenterY + layout.HalfGap;
        var outer = upper ? inner - layout.MaxHalfHeight : inner + layout.MaxHalfHeight;

        return upper
            ? new FillGradient(colors.BarFillSecondary, colors.BarFill, outer, inner)
            : new FillGradient(colors.BarFill, colors.BarFillSecondary, inner, outer);
    }

    private readonly record struct FillGradient(RgbaColor From, RgbaColor To, double StartY, double EndY);
}