using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Rendering;

/// <summary>
/// Неизменяемый снимок того, что рисуется в одном кадре: значения столбиков от 0 до 1
/// и оформление. Создаётся перед отрисовкой и дальше только читается, поэтому кадр можно
/// посчитать в одном потоке, а нарисовать в другом.
/// </summary>
public sealed class SpectrumSnapshot
{
    public SpectrumSnapshot(float[] values, ColorSettings colors, BarSettings bars)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(bars);

        Values = values;
        Colors = colors;
        Bars = bars;
    }

    public float[] Values { get; }

    public ColorSettings Colors { get; }

    public BarSettings Bars { get; }

    public int BarCount => Values.Length;

    public static SpectrumSnapshot Empty { get; } = new([], new ColorSettings(), new BarSettings());
}