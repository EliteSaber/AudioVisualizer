namespace AudioVisualizer.Core.Audio;

/// <summary>Декодированный трек: моно-сигнал в диапазоне [-1..1] с частотой дискретизации.</summary>
public sealed class AudioData
{
    public AudioData(float[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        Samples = samples;
        SampleRate = sampleRate;
    }

    public float[] Samples { get; }

    public int SampleRate { get; }

    public int Length => Samples.Length;

    public TimeSpan Duration => TimeSpan.FromSeconds(Samples.Length / (double)SampleRate);
}