namespace AudioVisualizer.Tests.Audio;

/// <summary>Тестовые сигналы для проверки фильтров: ровный синус без шума и разрывов.</summary>
internal static class TestTone
{
    /// <summary>Синус заданной частоты в диапазоне от -1 до 1.</summary>
    public static float[] Sine(int sampleRate, float frequencyHz, float amplitude, int length)
    {
        var samples = new float[length];

        for (var i = 0; i < length; i++)
        {
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * frequencyHz * i / sampleRate);
        }

        return samples;
    }

    /// <summary>Стерео-сигнал: слева одна частота, справа другая, сэмплы чересполосные.</summary>
    public static float[] Stereo(int sampleRate, float leftFrequencyHz, float rightFrequencyHz, float amplitude, int frames)
    {
        var samples = new float[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            samples[i * 2] = amplitude * MathF.Sin(2f * MathF.PI * leftFrequencyHz * i / sampleRate);
            samples[(i * 2) + 1] = amplitude * MathF.Sin(2f * MathF.PI * rightFrequencyHz * i / sampleRate);
        }

        return samples;
    }

    /// <summary>Один канал чересполосного сигнала.</summary>
    public static float[] Channel(ReadOnlySpan<float> interleaved, int channel, int channels = 2)
    {
        var frames = interleaved.Length / channels;
        var result = new float[frames];

        for (var i = 0; i < frames; i++)
        {
            result[i] = interleaved[(i * channels) + channel];
        }

        return result;
    }

    /// <summary>Среднеквадратичное отклонение отрезка сигнала.</summary>
    public static double Rms(ReadOnlySpan<float> samples, int start)
    {
        if (start < 0 || start >= samples.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Начало отрезка вне сигнала.");
        }

        var sum = 0d;

        for (var i = start; i < samples.Length; i++)
        {
            sum += (double)samples[i] * samples[i];
        }

        return Math.Sqrt(sum / (samples.Length - start));
    }
}
