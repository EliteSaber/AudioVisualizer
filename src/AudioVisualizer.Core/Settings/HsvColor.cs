namespace AudioVisualizer.Core.Settings;

/// <summary>
/// Цвет в HSV: тон, насыщенность и яркость. Нужна палитре выбора цвета — ползунки задают
/// каналы RGB, а поле насыщенности с яркостью и полоса тона работают с этой моделью.
/// </summary>
/// <remarks>
/// Значения нормализуются в конструкторе: тон лежит на круге в [0; 360), насыщенность и
/// яркость в [0; 1]. Поэтому перетаскивание за край поля не оставляет элемент в состоянии,
/// из которого цвет уже не собрать.
/// Альфа сюда не входит: HSV её не описывает, и её держит <see cref="RgbaColor"/>.
/// </remarks>
public readonly record struct HsvColor
{
    /// <summary>Полный круг тонов.</summary>
    public const double HueCircle = 360d;

    /// <summary>Стопов в градиенте полосы тона: по одному на каждые 10°.</summary>
    /// <remarks>
    /// Меньше 36 стопов дают заметные «переломы» между оттенками: градиент смешивает
    /// каналы в sRGB, а не в HSV, и на длинном участке проходит через не тот цвет.
    /// </remarks>
    public const int RampSteps = 37;

    public HsvColor(double hue, double saturation, double value)
    {
        Hue = WrapHue(hue);
        Saturation = Math.Clamp(saturation, 0d, 1d);
        Value = Math.Clamp(value, 0d, 1d);
    }

    /// <summary>Тон в градусах, 0 — красный.</summary>
    public double Hue { get; }

    /// <summary>Насыщенность: 0 — серый, 1 — чистый тон.</summary>
    public double Saturation { get; }

    /// <summary>Яркость, она же V в записи HSV.</summary>
    public double Value { get; }

    public static HsvColor FromRgba(RgbaColor color)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var span = max - min;

        // У серого и чёрного тона нет: подставлять 0 нужно явно, иначе при смене яркости
        // серый получил бы оттенок из остатка деления и поехал по кругу.
        if (span <= 0d)
        {
            return new HsvColor(0d, 0d, max);
        }

        var hue = max == red
            ? 60d * (((green - blue) / span) % 6d)
            : max == green
                ? 60d * (((blue - red) / span) + 2d)
                : 60d * (((red - green) / span) + 4d);

        return new HsvColor(hue, span / max, max);
    }

    public RgbaColor ToRgba(byte alpha)
    {
        var chroma = Value * Saturation;
        var second = chroma * (1d - Math.Abs((Hue / 60d) % 2d - 1d));
        var floor = Value - chroma;
        var (red, green, blue) = RgbSector(Hue, chroma, second);

        return new RgbaColor(Channel(red + floor), Channel(green + floor), Channel(blue + floor), alpha);
    }

    /// <summary>
    /// Цвета для градиента полосы тона: от красного к красному через круг, последний стоп
    /// совпадает с первым, иначе край полосы давал бы стык оттенков.
    /// </summary>
    public static IReadOnlyList<RgbaColor> HueRamp(int steps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 2);

        var colors = new RgbaColor[steps];

        for (var index = 0; index < steps; index++)
        {
            colors[index] = new HsvColor(index * HueCircle / (steps - 1), 1d, 1d).ToRgba(255);
        }

        return colors;
    }

    /// <summary>Три канала сектора тона до прибавления общего минимума.</summary>
    private static (double Red, double Green, double Blue) RgbSector(double hue, double chroma, double second)
    {
        var sector = Math.Floor(hue / 60d) % 6d;

        return sector switch
        {
            0d => (chroma, second, 0d),
            1d => (second, chroma, 0d),
            2d => (0d, chroma, second),
            3d => (0d, second, chroma),
            4d => (second, 0d, chroma),
            _ => (chroma, 0d, second)
        };
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255d), 0d, 255d);

    private static double WrapHue(double hue)
    {
        var wrapped = hue % HueCircle;

        return wrapped < 0d ? wrapped + HueCircle : wrapped;
    }
}