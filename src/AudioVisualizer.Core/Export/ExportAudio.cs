using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Export;

/// <summary>
/// Аудиодорожка ролика: те же полосы эквалайзера, что и в превью, и ограничение пика.
/// </summary>
public static class ExportAudio
{
    /// <summary>Предел громкости дорожки: выше него звук обрезается и трещит.</summary>
    public const float PeakLimit = 0.99f;

    /// <summary>
    /// Готовит моно-микс к выгрузке.
    /// </summary>
    /// <remarks>
    /// Исходный массив не трогается: из него же считается спектр кадров, а по спектру уже
    /// применён визуальный эквалайзер. Правка на месте применила бы его дважды, и картинка
    /// в ролике разошлась бы с тем, что человек видел на экране.
    /// </remarks>
    public static float[] PrepareMono(AudioData audio, SpectrumSettings spectrum)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(spectrum);

        if (!IsActive(spectrum))
        {
            return audio.Samples;
        }

        var copy = (float[])audio.Samples.Clone();
        var equalizer = new PeakEqualizer(audio.SampleRate, 1, spectrum.Equalizer);

        equalizer.Apply(copy);
        LimitPeak(copy);

        return copy;
    }

    /// <summary>
    /// Готовит звук с каналами: полосы эквалайзера и ограничение пика на месте.
    /// </summary>
    /// <remarks>
    /// Буфер принадлежит выгрузке и больше нигде не читается, поэтому копия не нужна.
    /// </remarks>
    public static void PrepareChannels(ChannelAudio audio, SpectrumSettings spectrum)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(spectrum);

        if (!IsActive(spectrum))
        {
            return;
        }

        var equalizer = new PeakEqualizer(audio.SampleRate, audio.Channels, spectrum.Equalizer);

        equalizer.Apply(audio.Samples);
        LimitPeak(audio.Samples);
    }

    /// <summary>
    /// Ограничивает пик всего сигнала: если он выше предела, весь трек масштабируется к нему.
    /// </summary>
    /// <remarks>
    /// Именно масштабирование, а не обрезка: обрезка слышна как треск на пиках, а ровная
    /// громкость сохраняет характер трека.
    /// </remarks>
    public static void LimitPeak(Span<float> samples)
    {
        var peak = 0f;

        foreach (var sample in samples)
        {
            var magnitude = Math.Abs(sample);

            if (magnitude > peak)
            {
                peak = magnitude;
            }
        }

        if (peak <= PeakLimit || peak <= 0f)
        {
            return;
        }

        var scale = PeakLimit / peak;

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= scale;
        }
    }

    /// <summary>
    /// Работает ли эквалайзер: галка включена и есть полоса с усилением.
    /// </summary>
    /// <remarks>
    /// Полосы с нулевым усилением не строятся вовсе, и без них эквалайзер ничего не
    /// меняет: лишний проход по треку и копия в памяти были бы впустую.
    /// </remarks>
    private static bool IsActive(SpectrumSettings spectrum)
    {
        if (!spectrum.ApplyEqualizerToAudio)
        {
            return false;
        }

        foreach (var band in spectrum.Equalizer)
        {
            if (Math.Abs(band.GainDb) >= PeakEqualizer.MinimumGainDb)
            {
                return true;
            }
        }

        return false;
    }
}
