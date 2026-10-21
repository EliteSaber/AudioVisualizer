using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

public class AudioLoaderTests
{
    private static float[] Sine(int sampleRate, int length, float amplitude)
    {
        var samples = new float[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * 1000f * i / sampleRate);
        }

        return samples;
    }

    private static TemporaryAudioFile MonoWavFile(int sampleRate = 44100, int length = 44100, float amplitude = 0.5f)
    {
        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, Sine(sampleRate, length, amplitude), sampleRate);
        return new TemporaryAudioFile(stream.ToArray());
    }

    [Fact]
    public void Load_ЧитаетWav_СВеркойЧастотыИДлины()
    {
        using var file = MonoWavFile(sampleRate: 8000, length: 8000);

        var result = new AudioLoader().Load(file.Path);

        Assert.Equal(8000, result.SampleRate);
        Assert.Equal(8000, result.Length);
        Assert.Equal(1f, result.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void Load_ЧитаетАмплитудуСигнала()
    {
        using var file = MonoWavFile(sampleRate: 8000, length: 8000, amplitude: 0.5f);

        var result = new AudioLoader().Load(file.Path);

        Assert.Equal(0.5f, result.Samples.Max(), 2);
    }

    [Fact]
    public void Load_СтереоWav_СводитВМоно()
    {
        // Левый канал 1.0, правый -1.0: среднее по кадрам должно быть нулём.
        var stereo = new short[8000 * 2];
        for (var frame = 0; frame < 8000; frame++)
        {
            stereo[frame * 2] = short.MaxValue;
            stereo[(frame * 2) + 1] = short.MinValue;
        }

        var content = RawWav.Build(format: 1, channels: 2, sampleRate: 8000, bitsPerSample: 16, RawWav.Pcm16(stereo));
        using var file = new TemporaryAudioFile(content);

        var result = new AudioLoader().Load(file.Path);

        Assert.Equal(8000, result.Length);
        Assert.All(result.Samples, sample => Assert.InRange(sample, -0.001f, 0.001f));
    }

    [Fact]
    public void LoadChannels_СтереоWav_СохраняетКаналы()
    {
        // Левый канал 1.0, правый -1.0. Свёрнутый в моно сигнал был бы нулём, а с
        // сохранёнными каналами ролик звучит как исходник.
        var stereo = new short[8000 * 2];
        for (var frame = 0; frame < 8000; frame++)
        {
            stereo[frame * 2] = short.MaxValue;
            stereo[(frame * 2) + 1] = short.MinValue;
        }

        var content = RawWav.Build(format: 1, channels: 2, sampleRate: 8000, bitsPerSample: 16, RawWav.Pcm16(stereo));
        using var file = new TemporaryAudioFile(content);

        var result = new AudioLoader().LoadChannels(file.Path);

        Assert.Equal(2, result.Channels);
        Assert.Equal(8000, result.FrameCount);
        Assert.Equal(1f, result.Samples[0], 3);
        Assert.Equal(-1f, result.Samples[1], 3);
    }

    [Fact]
    public void Load_ОтсутствующийФайл_ВыбрасываетFileNotFoundException()
        => Assert.Throws<FileNotFoundException>(() => new AudioLoader().Load(@"Z:\нет\такого.wav"));

    [Fact]
    public void Load_ПустойПуть_ВыбрасываетArgumentException()
        => Assert.Throws<ArgumentException>(() => new AudioLoader().Load("   "));

    [Fact]
    public void Load_НечитаемыйФайл_ВыбрасыщаетAudioDecodeException()
    {
        using var file = new TemporaryAudioFile([1, 2, 3, 4, 5, 6, 7, 8], extension: ".bin");

        Assert.Throws<AudioDecodeException>(() => new AudioLoader().Load(file.Path));
    }
}