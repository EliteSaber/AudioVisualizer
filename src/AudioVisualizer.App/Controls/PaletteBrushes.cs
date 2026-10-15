using System.Windows;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Кисти палитры выбора цвета. Отдельный класс по образцу <c>ColorBrushes</c> из Core: и
/// поле насыщенности с яркостью, и полоса тона рисуются одними и теми же заливками, и создают
/// их они одинаково — замороженными.
/// </summary>
internal static class PaletteBrushes
{
    /// <summary>Одноцветная кисть цвета настроек.</summary>
    public static SolidColorBrush Solid(RgbaColor color)
        => Frozen(new SolidColorBrush(ToColor(color)));

    /// <summary>
    /// Наложение одного цвета на другой по направлению <paramref name="end"/> в
    /// относительных координатах: 1 по X — слева направо, 1 по Y — сверху вниз.
    /// </summary>
    public static Brush Fade(Color from, Color to, Point end)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = end,
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };

        brush.GradientStops.Add(new GradientStop(from, 0d));
        brush.GradientStops.Add(new GradientStop(to, 1d));

        return Frozen(brush);
    }

    /// <summary>Полный круг тонов по горизонтали. Цвета берёт <see cref="HsvColor.HueRamp"/>.</summary>
    public static Brush HueRamp()
    {
        var colors = HsvColor.HueRamp(HsvColor.RampSteps);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(1d, 0d),
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };

        for (var index = 0; index < colors.Count; index++)
        {
            var color = colors[index];

            brush.GradientStops.Add(new GradientStop(
                Color.FromArgb(color.A, color.R, color.G, color.B),
                (double)index / (colors.Count - 1)));
        }

        return Frozen(brush);
    }

    /// <summary>Линия отметки: толщину задаёт <paramref name="thickness"/>.</summary>
    public static Pen Marker(Color color, double thickness)
        => Frozen(new Pen(Frozen(new SolidColorBrush(color)), thickness));

    private static Color ToColor(RgbaColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();

        return freezable;
    }
}