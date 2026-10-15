using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Rendering;

/// <summary>
/// Неизменяемый снимок того, что рисуется в одном кадре: значения столбиков от 0 до 1
/// и оформление. Создаётся перед отрисовкой и дальше только читается, поэтому кадр можно
/// посчитать в одном потоке, а нарисовать в другом.
/// </summary>
public sealed class SpectrumSnapshot
{
    public SpectrumSnapshot(float[] values, ColorSettings colors, BarSettings bars, int framesPerSecond = 60)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(bars);

        Values = values;
        Colors = colors;
        Bars = bars;
        FramesPerSecond = framesPerSecond > 0 ? framesPerSecond : 60;
    }

    public float[] Values { get; }

    public ColorSettings Colors { get; }

    public BarSettings Bars { get; }

    /// <summary>
    /// Частота кадров, с которой снимок сменяет предыдущий. Нужна следу: он затухает по времени,
    /// а по кадрам его длина зависела бы от частоты кадров ролика.
    /// </summary>
    public int FramesPerSecond { get; }

    public int BarCount => Values.Length;

    public static SpectrumSnapshot Empty { get; } = new([], new ColorSettings(), new BarSettings());
}