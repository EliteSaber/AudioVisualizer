namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Фильтр второго порядка с усилением на центральной частоте: peaking EQ по рецепту RBJ.
/// </summary>
/// <remarks>
/// Форма — транспонированная direct form II: десять таких фильтров подряд на прямой форме
/// теряют точность на низких частотах, и сумма полос начинает «плыть».
/// </remarks>
public struct BiquadFilter
{
    private float _b0;
    private float _b1;
    private float _b2;
    private float _a1;
    private float _a2;

    // Состояние фильтра: две задержки между сэмплами.
    private float _s1;
    private float _s2;

    /// <summary>
    /// Фильтр с усилением на центральной частоте.
    /// </summary>
    /// <param name="sampleRate">Частота дискретизации, Гц.</param>
    /// <param name="centerFrequencyHz">Центральная частота, Гц. Должна быть ниже Найквиста.</param>
    /// <param name="quality">Добротность: больше - уже полоса.</param>
    /// <param name="gainDb">Усиление на центре, дБ.</param>
    public static BiquadFilter Peaking(float sampleRate, float centerFrequencyHz, float quality, float gainDb)
    {
        var filter = default(BiquadFilter);

        filter.SetPeaking(sampleRate, centerFrequencyHz, quality, gainDb);

        return filter;
    }

    /// <summary>
    /// Задаёт коэффициенты полосы с усилением. Состояние фильтра при этом сохраняется:
    /// полосы меняются ползунком на ходу, и сброс состояния был бы щелчком.
    /// </summary>
    public void SetPeaking(float sampleRate, float centerFrequencyHz, float quality, float gainDb)
    {
        if (sampleRate <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        if (centerFrequencyHz <= 0f || centerFrequencyHz >= sampleRate / 2f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(centerFrequencyHz),
                centerFrequencyHz,
                "Центральная частота должна быть выше нуля и ниже Найквиста.");
        }

        if (quality <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Добротность должна быть положительной.");
        }

        // A = 10^(dB/40): усиление на центре получается вдвое больше заданного в децибелах,
        // потому что полоса поднимает оба склона, а не только вершину.
        var a = Math.Pow(10d, gainDb / 40d);
        var w0 = 2d * Math.PI * centerFrequencyHz / sampleRate;
        var alpha = Math.Sin(w0) / (2d * quality);
        var cos = Math.Cos(w0);

        var b0 = 1d + (alpha * a);
        var b1 = -2d * cos;
        var b2 = 1d - (alpha * a);
        var a0 = 1d + (alpha / a);
        var a1 = -2d * cos;
        var a2 = 1d - (alpha / a);

        _b0 = (float)(b0 / a0);
        _b1 = (float)(b1 / a0);
        _b2 = (float)(b2 / a0);
        _a1 = (float)(a1 / a0);
        _a2 = (float)(a2 / a0);
    }

    /// <summary>Прогоняет один сэмпл через фильтр.</summary>
    public float Process(float sample)
    {
        var output = (_b0 * sample) + _s1;

        _s1 = (_b1 * sample) - (_a1 * output) + _s2;
        _s2 = (_b2 * sample) - (_a2 * output);

        return output;
    }

    /// <summary>
    /// Сбрасывает состояние. Фильтр начинает считать заново, и на стыке слышен щелчок,
    /// поэтому состояние сбрасывают только при пересборке цепочки, а не при смене полос.
    /// </summary>
    public void ResetState()
    {
        _s1 = 0f;
        _s2 = 0f;
    }
}
