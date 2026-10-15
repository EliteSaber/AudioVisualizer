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

    /// <summary>
    /// Нечисловой сэмпл становится тишиной.
    /// </summary>
    /// <remarks>
    /// Ловушка именно в <see cref="Math.Clamp(float, float, float)"/>: при NaN оба сравнения
    /// ложны, и значение проходит насквозь. Дальше <c>(short)float.NaN</c> в C# не определено
    /// и на практике даёт скачок полной громкости вместо тишины. Такой сэмпл доехал бы и до
    /// столбиков спектра, поэтому проверка стоит на входе, а не только при записи в WAV.
    /// </remarks>
    public static float Finite(float sample) => float.IsFinite(sample) ? sample : 0f;
}