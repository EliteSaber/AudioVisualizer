using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Analysis;

/// <summary>
/// Визуальный эквалайзер: прибавляет усиление полос к значениям спектра в децибелах.
/// Между центрами полос усиление интерполируется по логарифмической шкале частот.
/// </summary>
public sealed class EqualizerProcessor
{
    private readonly float[] _gainPerBand;

    public EqualizerProcessor(IReadOnlyList<float> bandCenterFrequencies, IReadOnlyList<EqualizerBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bandCenterFrequencies);
        ArgumentNullException.ThrowIfNull(bands);

        if (bands.Count > 1)
        {
            for (var i = 1; i < bands.Count; i++)
            {
                if (bands[i].CenterFrequencyHz <= bands[i - 1].CenterFrequencyHz)
                {
                    throw new ArgumentException("Полосы эквалайзера должны быть отсортированы по возрастанию частоты.", nameof(bands));
                }
            }
        }

        _gainPerBand = new float[bandCenterFrequencies.Count];
        for (var i = 0; i < _gainPerBand.Length; i++)
        {
            _gainPerBand[i] = GainAt(bands, bandCenterFrequencies[i]);
        }
    }

    /// <summary>Усиление в децибелах для произвольной частоты: между центрами полос линейно по лог-шкале, вне диапазона - крайняя полоса.</summary>
    public static float GainAt(IReadOnlyList<EqualizerBand> bands, float frequencyHz)
    {
        ArgumentNullException.ThrowIfNull(bands);

        if (bands.Count == 0)
        {
            return 0f;
        }

        if (frequencyHz <= bands[0].CenterFrequencyHz)
        {
            return bands[0].GainDb;
        }

        var last = bands.Count - 1;
        if (frequencyHz >= bands[last].CenterFrequencyHz)
        {
            return bands[last].GainDb;
        }

        var upper = 1;
        while (upper < last && bands[upper].CenterFrequencyHz < frequencyHz)
        {
            upper++;
        }

        var lower = upper - 1;
        var lowLog = MathF.Log10(bands[lower].CenterFrequencyHz);
        var highLog = MathF.Log10(bands[upper].CenterFrequencyHz);
        var position = Math.Clamp((MathF.Log10(frequencyHz) - lowLog) / (highLog - lowLog), 0f, 1f);

        return bands[lower].GainDb + ((bands[upper].GainDb - bands[lower].GainDb) * position);
    }

    public void Apply(Span<float> decibels)
    {
        if (decibels.Length != _gainPerBand.Length)
        {
            throw new ArgumentException($"Ожидалось {_gainPerBand.Length} значений спектра, получено {decibels.Length}.", nameof(decibels));
        }

        for (var i = 0; i < decibels.Length; i++)
        {
            decibels[i] += _gainPerBand[i];
        }
    }
}