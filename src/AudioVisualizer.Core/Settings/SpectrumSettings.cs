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

    /// <summary>
    /// Меняют ли полосы эквалайзера звук: в превью и в аудиодорожке выгруженного ролика.
    /// </summary>
    /// <remarks>
    /// Живёт рядом с полосами, а не в <see cref="ExportSettings"/>: тот уезжает в пресеты
    /// ролика целиком, а полосы в пресеты не попадают, и пресет «эквалайзер в звуке» без
    /// значений полос был бы враньём.
    /// <para>
    /// В сравнение настроек спектра не входит: равенство решает, пересобирать ли источник
    /// кадров, а флаг спектр не меняет - как не меняют его атака и спад.
    /// </para>
    /// </remarks>
    public bool ApplyEqualizerToAudio { get; init; }

    public static SpectrumSettings Default { get; } = new();

    /// <summary>
    /// Равенство по значению, а не по ссылке: список полос эквалайзера каждый раз новый,
    /// и без этого настройки, отличающиеся только содержимым списка, считались бы разными.
    /// </summary>
    public bool Equals(SpectrumSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return FftSize == other.FftSize
            && AnalysisBandCount == other.AnalysisBandCount
            && MinFrequencyHz.Equals(other.MinFrequencyHz)
            && MaxFrequencyHz.Equals(other.MaxFrequencyHz)
            && MinDecibels.Equals(other.MinDecibels)
            && MaxDecibels.Equals(other.MaxDecibels)
            && GainDb.Equals(other.GainDb)
            && Gamma.Equals(other.Gamma)
            && Attack.Equals(other.Attack)
            && Release.Equals(other.Release)
            && BandsAreEqual(Equalizer, other.Equalizer);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FftSize);
        hash.Add(AnalysisBandCount);
        hash.Add(MinFrequencyHz);
        hash.Add(MaxFrequencyHz);
        hash.Add(MinDecibels);
        hash.Add(MaxDecibels);
        hash.Add(GainDb);
        hash.Add(Gamma);
        hash.Add(Attack);
        hash.Add(Release);

        foreach (var band in Equalizer)
        {
            hash.Add(band);
        }

        return hash.ToHashCode();
    }

    private static bool BandsAreEqual(IReadOnlyList<EqualizerBand>? left, IReadOnlyList<EqualizerBand>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}