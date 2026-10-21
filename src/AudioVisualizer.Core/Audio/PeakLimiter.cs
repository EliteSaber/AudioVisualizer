namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Ограничитель пика для превью: не даёт усиленной полосе переполнить выход.
/// </summary>
/// <remarks>
/// Урезание мгновенное, а возврат громкости плавный: так сигнал никогда не выходит за
/// предел, и при этом не «дышит» на каждом пике. В выгрузке ограничение другое, там весь
/// трек масштабируется к пику целиком, - там пик известен заранее, а здесь сигнал идёт
/// потоком и будущего пика не видно.
/// </remarks>
public sealed class PeakLimiter
{
    /// <summary>Предел, до которого ограничитель пропускает сигнал.</summary>
    public const float DefaultLimit = 0.99f;

    private readonly float _limit;
    private readonly float _releaseCoef;
    private float _gain = 1f;

    /// <param name="sampleRate">Частота дискретизации, Гц.</param>
    /// <param name="limit">Предел громкости.</param>
    /// <param name="releaseSeconds">Время возврата громкости после пика, секунды.</param>
    public PeakLimiter(int sampleRate, float limit = DefaultLimit, double releaseSeconds = 0.02d)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        if (limit <= 0f || limit > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Предел должен быть от нуля до единицы.");
        }

        if (releaseSeconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(releaseSeconds), releaseSeconds, "Время возврата должно быть положительным.");
        }

        _limit = limit;
        _releaseCoef = (float)(1d - Math.Exp(-1d / (releaseSeconds * sampleRate)));
    }

    /// <summary>Прогоняет один сэмпл через ограничитель.</summary>
    public float Process(float sample)
    {
        var value = AudioData.Finite(sample);

        return value * UpdateGain(Math.Abs(value));
    }

    /// <summary>
    /// Ограничивает блок сэмплов. Громкость у каналов одна: пик ищется по всем каналам
    /// кадра, иначе левый канал приглушал бы правый.
    /// </summary>
    /// <param name="samples">Сэмплы чересполосно.</param>
    /// <param name="channels">Число каналов.</param>
    public void Apply(Span<float> samples, int channels)
    {
        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), channels, "Число каналов должно быть положительным.");
        }

        var frames = samples.Length / channels;

        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * channels;
            var peak = 0f;

            for (var channel = 0; channel < channels; channel++)
            {
                peak = Math.Max(peak, Math.Abs(samples[offset + channel]));
            }

            var gain = UpdateGain(peak);

            for (var channel = 0; channel < channels; channel++)
            {
                samples[offset + channel] *= gain;
            }
        }
    }

    /// <summary>Возвращает громкость к полной: начало трека не должно начинаться с приглушённого звука.</summary>
    public void Reset() => _gain = 1f;

    /// <summary>
    /// Пересчитывает громкость по пику. Урезание мгновенное, а возврат плавный: так сигнал
    /// никогда не выходит за предел, и при этом не «дышит» на каждом пике.
    /// </summary>
    private float UpdateGain(float magnitude)
    {
        if (magnitude > 0f && (magnitude * _gain) >= _limit)
        {
            // Урезаем сразу: мягкое нарастание ограничения дало бы выход за предел
            // на первых сэмплах пика, и там ровно там, где звук должен был бы упасть.
            _gain = _limit / magnitude;
        }
        else
        {
            _gain += (1f - _gain) * _releaseCoef;
        }

        return _gain;
    }
}
