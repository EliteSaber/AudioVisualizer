namespace AudioVisualizer.Tests.Analysis;

/// <summary>Синтез тестовых сигналов: синус точно на частоте бина, чтобы пик не размазывался утечкой.</summary>
internal static class TestSignal
{
    public static float[] SineOnBin(int sampleRate, int bin, int fftSize, int length, float amplitude)
    {
        var frequency = bin * sampleRate / (float)fftSize;

        var samples = new float[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * frequency * i / sampleRate);
        }

        return samples;
    }
}