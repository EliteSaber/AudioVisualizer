using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

/// <summary>
/// Трек с сохранёнными каналами: свод для спектра и звук ролика считаются из одного
/// и того же трека, и свод должен сходиться с тем, что даёт моно-путь.
/// </summary>
public class ChannelAudioTests
{
    [Fact]
    public void СводКаналов_СчитаетсяПоСреднему()
    {
        // Левый канал 1.0, правый -1.0: среднее по кадрам равно нулю.
        var audio = new ChannelAudio([1f, -1f, 1f, -1f], 2, 44100);

        var mix = audio.ToMonoMix();

        Assert.Equal([0f, 0f], mix);
    }

    [Fact]
    public void СводСовпадаетСМоноПутёмРазбораWav()
    {
        // Моно-микс у спектра и свод каналов у ролика должны совпадать: иначе картинка
        // в ролике считалась бы по одному сигналу, а звучал бы ролик по другому.
        float[] original = [0.5f, -0.5f, 0.25f, -0.25f, 0f, 0f];

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, original, 44100, 2);

        stream.Position = 0;
        var mono = WavCodec.Read(stream);

        stream.Position = 0;
        var channels = WavCodec.ReadInterleaved(stream);

        Assert.Equal(2, channels.Channels);
        Assert.Equal(mono.Length, channels.FrameCount);

        for (var i = 0; i < mono.Length; i++)
        {
            Assert.InRange(channels.ToMonoMix()[i], mono.Samples[i] - 0.0001f, mono.Samples[i] + 0.0001f);
        }
    }

    [Fact]
    public void ОдинКанал_СводитсяБезИзменений()
    {
        var audio = new ChannelAudio([0.5f, -0.25f], 1, 44100);

        Assert.Equal([0.5f, -0.25f], audio.ToMonoMix());
    }

    [Fact]
    public void Длительность_СчитаетсяПоКадрам()
    {
        var audio = new ChannelAudio(new float[44100 * 2], 2, 44100);

        Assert.Equal(1d, audio.Duration.TotalSeconds, 3);
        Assert.Equal(44100, audio.FrameCount);
    }

    [Fact]
    public void ЧислоСэмпловНеДелитсяНаКаналы_БросаетИсключение()
        => Assert.Throws<ArgumentException>(() => new ChannelAudio([1f, 2f, 3f], 2, 44100));

    [Fact]
    public void НольКаналов_БросаетИсключение()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ChannelAudio([], 0, 44100));

    [Fact]
    public void НулеваяЧастота_БросаетИсключение()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ChannelAudio([], 1, 0));
}
