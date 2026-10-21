namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Декодированный трек с сохранёнными каналами: сэмплы идут чересполосно, попарно на кадр.
/// </summary>
/// <remarks>
/// Моно-микс нужен спектру, а звук ролика должен быть как в исходнике. Поэтому трек
/// декодируется с каналами, а свод для спектра считается отдельно.
/// </remarks>
public sealed class ChannelAudio
{
    public ChannelAudio(float[] samples, int channels, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), channels, "Число каналов должно быть положительным.");
        }

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        if (samples.Length % channels != 0)
        {
            throw new ArgumentException(
                $"Число сэмплов ({samples.Length}) не делится на число каналов ({channels}).",
                nameof(samples));
        }

        Samples = samples;
        Channels = channels;
        SampleRate = sampleRate;
    }

    /// <summary>Сэмплы чересполосно: кадр занимает <see cref="Channels"/> чисел подряд.</summary>
    public float[] Samples { get; }

    public int Channels { get; }

    public int SampleRate { get; }

    /// <summary>Число кадров: групп сэмплов по одному на канал.</summary>
    public int FrameCount => Samples.Length / Channels;

    public TimeSpan Duration => TimeSpan.FromSeconds(FrameCount / (double)SampleRate);

    /// <summary>
    /// Свод каналов в один сигнал: среднее по каналам. Так же считается моно-микс,
    /// на котором построен спектр, - свод с потерей на 3 дБ и фазовым вычитанием
    /// на разнесённых дорожках.
    /// </summary>
    public float[] ToMonoMix()
    {
        if (Channels == 1)
        {
            return (float[])Samples.Clone();
        }

        var mix = new float[FrameCount];

        for (var frame = 0; frame < FrameCount; frame++)
        {
            var offset = frame * Channels;
            var sum = 0f;

            for (var channel = 0; channel < Channels; channel++)
            {
                sum += Samples[offset + channel];
            }

            mix[frame] = AudioData.Finite(sum / Channels);
        }

        return mix;
    }
}
