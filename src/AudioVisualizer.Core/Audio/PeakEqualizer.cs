using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Эквалайзер: набор полос с усилением, каждая - фильтр peaking по центру полосы.
/// </summary>
/// <remarks>
/// Фильтры строятся по одному на канал: у стерео левый и правый каналы считаются отдельно,
/// иначе «стерео» оказалось бы моно с чередующимися сэмплами.
/// Полосы с нулевым усилением не строятся вовсе: peaking с нулевым усилением пропускает
/// сигнал по амплитуде, но всё равно сдвигает фазу.
/// </remarks>
public sealed class PeakEqualizer
{
    /// <summary>
    /// Добротность по умолчанию. Полосы идут через октаву, и 1,41 - это примерно октава
    /// шириной: соседние полосы почти не наезжают друг на друга.
    /// </summary>
    public const float DefaultQuality = 1.41f;

    /// <summary>Усиление меньше этого считается нулём: полоса не строится.</summary>
    public const float MinimumGainDb = 0.01f;

    private readonly int _sampleRate;
    private readonly int _channels;
    private Band[] _bands;

    /// <param name="sampleRate">Частота дискретизации, Гц.</param>
    /// <param name="channels">Число каналов. Сигнал чересполосный.</param>
    /// <param name="bands">Полосы эквалайзера: центральная частота и усиление.</param>
    public PeakEqualizer(int sampleRate, int channels, IReadOnlyList<EqualizerBand> bands)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), channels, "Число каналов должно быть положительным.");
        }

        _sampleRate = sampleRate;
        _channels = channels;
        _bands = [];

        UpdateBands(bands);
    }

    /// <summary>Есть ли в эквалайзере хоть одна полоса: иначе сигнал идёт мимо фильтров.</summary>
    public bool HasEffect => _bands.Length > 0;

    /// <summary>Есть ли полоса с усилением вверх: только тогда сигнал может переполниться.</summary>
    public bool HasBoost
    {
        get
        {
            foreach (var band in _bands)
            {
                if (band.GainDb > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Пересчитывает полосы. Состояние фильтров сохраняется: сбросить его - значит щелчок
    /// на живом воспроизведении, а полосы меняются ползунком на ходу.
    /// </summary>
    public void UpdateBands(IReadOnlyList<EqualizerBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);

        var updated = new List<Band>();

        foreach (var band in bands)
        {
            if (Math.Abs(band.GainDb) < MinimumGainDb)
            {
                continue;
            }

            // Полоса выше Найквиста построить нельзя: коэффициенты у неё вырожденные,
            // и такой фильтр кладёт звук вместо того, чтобы его менять.
            if (band.CenterFrequencyHz <= 0f || band.CenterFrequencyHz >= _sampleRate / 2f)
            {
                continue;
            }

            var existing = FindByFrequency(updated, band.CenterFrequencyHz);

            if (existing is null)
            {
                existing = new Band(band.CenterFrequencyHz, band.GainDb, _channels);
                updated.Add(existing);
            }
            else
            {
                existing.GainDb = band.GainDb;
            }

            for (var channel = 0; channel < _channels; channel++)
            {
                existing.Filters[channel].SetPeaking(
                    _sampleRate,
                    band.CenterFrequencyHz,
                    DefaultQuality,
                    band.GainDb);
            }
        }

        _bands = [.. updated];
    }

    /// <summary>
    /// Пропускает сигнал через полосы. Сэмплы чересполосные, и на месте: буфер приходит
    /// из декодера или из превью, и копия ради копии не нужна.
    /// </summary>
    public void Apply(Span<float> samples)
    {
        if (_bands.Length == 0)
        {
            return;
        }

        var frames = samples.Length / _channels;

        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * _channels;

            for (var channel = 0; channel < _channels; channel++)
            {
                var value = samples[offset + channel];

                foreach (var band in _bands)
                {
                    value = band.Filters[channel].Process(value);
                }

                samples[offset + channel] = value;
            }
        }
    }

    private static Band? FindByFrequency(List<Band> bands, float frequencyHz)
    {
        foreach (var band in bands)
        {
            if (Math.Abs(band.FrequencyHz - frequencyHz) < 0.01f)
            {
                return band;
            }
        }

        return null;
    }

    /// <summary>Полоса: частота, усиление и по фильтру на каждый канал.</summary>
    private sealed class Band(float frequencyHz, float gainDb, int channels)
    {
        public float FrequencyHz { get; } = frequencyHz;

        public float GainDb { get; set; } = gainDb;

        public BiquadFilter[] Filters { get; } = new BiquadFilter[channels];
    }
}
