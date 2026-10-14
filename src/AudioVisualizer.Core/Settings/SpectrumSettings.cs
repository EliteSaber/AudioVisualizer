namespace AudioVisualizer.Core.Settings;

/// <summary>Настройки расчёта спектра и его визуального эквалайзера.</summary>
public sealed record SpectrumSettings
{
    public static IReadOnlyList<EqualizerBand> DefaultEqualizer { get; } =
    [
        new(31.5f, 0f),
        new(63f, 0f),
        new(125f, 0f),
        new(250f, 0f),
        new(500f, 0f),
        new(1000f, 0f),
        new(2000f, 0f),
        new(4000f, 0f),
        new(8000f, 0f),
        new(16000f, 0f)
    ];

    public int FftSize { get; init; } = 4096;

    /// <summary>Число полос анализа. Не зависит от числа столбиков: столбики получают пиковые значения из этих полос.</summary>
    public int AnalysisBandCount { get; init; } = 256;

    public float MinFrequencyHz { get; init; } = 30f;

    public float MaxFrequencyHz { get; init; } = 16000f;

    public float MinDecibels { get; init; } = -70f;

    public float MaxDecibels { get; init; } = -10f;

    public float GainDb { get; init; }

    /// <summary>Степень нелинейности высоты столбика: меньше единицы растягивает тихие значения вверх.</summary>
    public float Gamma { get; init; } = 1f;

    /// <summary>Коэффициент сглаживания при росте значения полосы, от 0 до 1.</summary>
    public float Attack { get; init; } = 0.6f;

    /// <summary>Коэффициент сглаживания при спаде значения полосы, от 0 до 1.</summary>
    public float Release { get; init; } = 0.2f;

    public IReadOnlyList<EqualizerBand> Equalizer { get; init; } = DefaultEqualizer;

    public static SpectrumSettings Default { get; } = new();
}