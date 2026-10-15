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
    /// Призрак прежней высоты столбика. Рисуется целиком, от края зазора до прежней вершины,
    /// и закрывается текущим столбиком, который рисуется поверх.
    /// </summary>
    /// <remarks>
    /// Раньше здесь была полоса между прежней и нынешней вершинами, и это была неверная форма:
    /// она обрывалась ровно на вершине столбика, а купол ниже вершины расходится в стороны,
    /// и между полосой и столбиком оставался пустой фон. Призрак же занимает и эту область,
    /// поэтому след выглядит продолжением столбика, а не накладкой сверху. Форма и радиус
    /// берутся те же, что у столбика, - это тот же столбик, только прежней высоты. Пером
    /// призрак не обводится: у него по краям прозрачность, обводить нечего.
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

        // Призрак, не отличающийся от столбика, виден не будет: он целиком под ним. Порог в
        // пикселях, а не в долях высоты, иначе у полки над столбиком след был бы незаметен.
        var density = snapshot.Bars.TrailDensity;

        if (density <= 0d || past.Width <= 0d || ((ghost - value) * layout.MaxHalfHeight) < BarTrail.MinimumVisibleHeight)
        {
            return;
        }

        var span = FillSpan(snapshot, layout, upper);
        var seam = upper ? current.Y : current.Bottom;

        // Непрозрачность достигается на вершине столбика, а не у самого края зазора: ниже
        // вершины след виден по бокам от купола, и полупрозрачный там читался бы просветом.
        var opaqueAt = (seam - past.Y) / past.Height;
        var color = ColorBrushes.At(span.From, span.To, span.StartY, span.EndY, seam).Faded(density);

        drawingContext.DrawGeometry(
            ColorBrushes.Fade(color, transparentAtEnd: !upper, opaqueAt),
            null,
            BuildGhost(past, current, snapshot, upper));
    }

    /// <summary>
    /// Геометрия призрака: тот же столбик, только прежней высоты.
    /// </summary>
    /// <remarks>
    /// Из столбика вычитается его собственный контур, но только когда заливка столбика
    /// полупрозрачна: иначе след просвечивал бы сквозь него и подкрашивал его. Считать эту
    /// разность всегда нельзя - булевы операции над путями с дугами дороже, чем заливка той
    /// площади, которая под столбиком и так не видна.
    /// </remarks>
    private static Geometry BuildGhost(Rect ghost, Rect bar, SpectrumSnapshot snapshot, bool upper)
    {
        var settings = snapshot.Bars;
        var colors = snapshot.Colors;
        var geometry = BarGeometry.Build(ghost, settings.CornerRadius, settings.CornerStyle, outerCornersAtTop: upper);

        if (colors.BarFill.A >= 255 && colors.BarFillSecondary.A >= 255)
        {
            return geometry;
        }

        var difference = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            geometry,
            BarGeometry.Build(bar, settings.CornerRadius, settings.CornerStyle, outerCornersAtTop: upper));

        difference.Freeze();

        return difference;
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