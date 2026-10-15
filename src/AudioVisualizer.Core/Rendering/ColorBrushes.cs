using System.Windows;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Rendering;

internal static class ColorBrushes
{
    public static SolidColorBrush Solid(RgbaColor color) => Frozen(new SolidColorBrush(ToColor(color)));

    /// <summary>Градиент от цента строки к краю столбика: по оси Y, от точки start к end.</summary>
    public static LinearGradientBrush Vertical(RgbaColor near, RgbaColor far, Point start, Point end)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end,
            MappingMode = BrushMappingMode.Absolute
        };

        brush.GradientStops.Add(new GradientStop(ToColor(near), 0d));
        brush.GradientStops.Add(new GradientStop(ToColor(far), 1d));
        brush.Freeze();

        return brush;
    }

    /// <summary>
    /// Затухание цвета в прозрачность по краям полосы, растянутое по её габаритам: начало и
    /// конец градиента - края полосы, поэтому след одинаково гаснет у столбиков разной высоты.
    /// </summary>
    /// <param name="transparentAtEnd">Куда уходит прозрачность: в начало полосы или в конец.</param>
    public static LinearGradientBrush Fade(RgbaColor color, bool transparentAtEnd)
    {
        var opaque = ToColor(color);

        // Гаснет не в чёрный, а в прозрачность своего же цвета: иначе по краю следа тянулась
        // бы серая полоса от смешивания с прозрачным чёрным.
        var transparent = Color.FromArgb(0, color.R, color.G, color.B);

        // Направление всегда сверху вниз, а переворачивается только порядок цветов: у нижней
        // половины столбика прежняя вершина лежит снизу, и прозрачность уходит в конец полосы.
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(0d, 1d),
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };

        brush.GradientStops.Add(new GradientStop(transparentAtEnd ? opaque : transparent, 0d));
        brush.GradientStops.Add(new GradientStop(transparentAtEnd ? transparent : opaque, 1d));
        brush.Freeze();

        return brush;
    }

    /// <summary>
    /// Цвет вертикального градиента из <paramref name="from"/> в <paramref name="to"/> на высоте
    /// <paramref name="y"/>. Нужен, чтобы след смыкался со столбиком тем же цветом, каким сам
    /// столбик на этой высоте.
    /// </summary>
    public static RgbaColor At(RgbaColor from, RgbaColor to, double startY, double endY, double y)
    {
        var span = endY - startY;

        if (span <= 0d)
        {
            return from;
        }

        var t = Math.Clamp((y - startY) / span, 0d, 1d);

        return new RgbaColor(
            Mix(from.R, to.R, t),
            Mix(from.G, to.G, t),
            Mix(from.B, to.B, t),
            Mix(from.A, to.A, t));
    }

    private static byte Mix(byte from, byte to, double t)
        => (byte)Math.Clamp(Math.Round(from + ((to - from) * t)), 0d, 255d);

    private static Color ToColor(RgbaColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static SolidColorBrush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();

        return brush;
    }
}