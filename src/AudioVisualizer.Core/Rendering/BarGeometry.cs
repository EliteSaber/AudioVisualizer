using System.Windows;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Rendering;

/// <summary>Геометрия одного столбика под выбранный вид углов.</summary>
public static class BarGeometry
{
    public static Geometry Build(Rect rect, double radius, CornerStyle cornerStyle, bool outerCornersAtTop)
    {
        if (rect.IsEmpty)
        {
            return Geometry.Empty;
        }

        // WPF схлопывает радиус больше половины меньшей стороны, но считает это уже
        // не скруглением: ограничиваем сами, чтобы геометрия совпадала с расчётом.
        var effectiveRadius = Math.Clamp(radius, 0d, Math.Min(rect.Width, rect.Height) / 2d);

        Geometry geometry = cornerStyle switch
        {
            CornerStyle.Rectangular => Rect(rect),
            CornerStyle.Rounded => Freeze(new RectangleGeometry(rect, effectiveRadius, effectiveRadius)),
            _ => Freeze(BuildOuterRounded(rect, effectiveRadius, outerCornersAtTop))
        };

        return geometry;
    }

    /// <summary>Прямой прямоугольник без скругления: полоса следа уходит в прозрачность, углы не видны.</summary>
    public static Geometry Rect(Rect rect) => Freeze(new RectangleGeometry(rect));

    /// <summary>
    /// Столбик со скруглёнными только внешними углами: у центральной линии углы прямые,
    /// иначе столбики выглядят разорванными. Обход контура идёт по часовой стрелке,
    /// поэтому все дуги имеют один и тот же SweepDirection.
    /// </summary>
    private static T Freeze<T>(T geometry)
        where T : Geometry
    {
        // Замороженная геометрия рисуется быстрее и не привязана к потоку: кадры считает
        // один поток, а рисует другой.
        geometry.Freeze();

        return geometry;
    }

    private static StreamGeometry BuildOuterRounded(Rect rect, double radius, bool outerCornersAtTop)
    {
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            var x = rect.X;
            var y = rect.Y;
            var right = rect.Right;
            var bottom = rect.Bottom;

            if (outerCornersAtTop)
            {
                context.BeginFigure(new Point(x, bottom), isFilled: true, isClosed: true);
                context.LineTo(new Point(x, y + radius), isStroked: true, isSmoothJoin: false);
                context.ArcTo(new Point(x + radius, y), new Size(radius, radius), 0d, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(right - radius, y), isStroked: true, isSmoothJoin: false);
                context.ArcTo(new Point(right, y + radius), new Size(radius, radius), 0d, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(right, bottom), isStroked: true, isSmoothJoin: false);
            }
            else
            {
                context.BeginFigure(new Point(x, y), isFilled: true, isClosed: true);
                context.LineTo(new Point(right, y), isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(right, bottom - radius), isStroked: true, isSmoothJoin: false);
                context.ArcTo(new Point(right - radius, bottom), new Size(radius, radius), 0d, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(x + radius, bottom), isStroked: true, isSmoothJoin: false);
                context.ArcTo(new Point(x, bottom - radius), new Size(radius, radius), 0d, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
            }
        }

        return Freeze(geometry);
    }
}