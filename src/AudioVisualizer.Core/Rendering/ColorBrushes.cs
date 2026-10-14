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

    private static Color ToColor(RgbaColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static SolidColorBrush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();

        return brush;
    }
}