using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

public class FfmpegAudioDecoderTests
{
    /// <summary>Проверки запускают настоящий процесс ffmpeg; если его нет в системе, тест ничего не проверяет.</summary>
    private static string? LocateFfmpeg() => new FfmpegLocator().Locate();

    private static TemporaryAudioFile MonoWavFile(int sampleRate = 44100, int length = 44100, float amplitude = 0.5f)
    {
        var samples = new float[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * 1000f * i / sampleRate);
        }

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, sampleRate);
        return new TemporaryAudioFile(stream.ToArray());
    }

    [Fact]
    public void Decode_ЧитаетWav_СВеркойЧастотыИДлины()
    {
        var ffmpeg = LocateFfmpeg();
        if (ffmpeg is null)
        {
            return;
        }

        using var file = MonoWavFile(sampleRate: 8000, length: 8000);

        var result = FfmpegAudioDecoder.Decode(file.Path, ffmpeg);

        Assert.Equal(8000, result.SampleRate);
        Assert.InRange(result.Length, 7999, 8001);
    }

    [Fact]
    public void Decode_СводитСтереоКMono()
    {
        var ffmpeg = LocateFfmpeg();
        if (ffmpeg is null)
        {
            return;
        }

        // Первый кадр: левый 1.0, правый 1.0. Второй: левый 1.0, правый 0.5.
        // Свод в моно делает WavCodec усреднением каналов.
        var stereo = new short[4];
        stereo[0] = short.MaxValue;
        stereo[1] = short.MaxValue;
        stereo[2] = short.MaxValue;
        stereo[3] = (short)(short.MaxValue / 2);

        var content = RawWav.Build(format: 1, channels: 2, sampleRate: 8000, bitsPerSample: 16, RawWav.Pcm16(stereo));
        using var file = new TemporaryAudioFile(content);

        var result = FfmpegAudioDecoder.Decode(file.Path, ffmpeg);

        Assert.Equal(8000, result.SampleRate);
        Assert.Equal(2, result.Length);
        Assert.InRange(result.Samples[0], 0.99f, 1.01f);
        Assert.InRange(result.Samples[1], 0.74f, 0.76f);
    }

    [Fact]
    public void Decode_НечитаемыйФайл_ВыбрасываетAudioDecodeException()
    {
        var ffmpeg = LocateFfmpeg();
        if (ffmpeg is null)
        {
            return;
        }

        using var file = new TemporaryAudioFile([1, 2, 3, 4, 5, 6, 7, 8], extension: ".bin");

        var exception = Assert.Throws<AudioDecodeException>(() => FfmpegAudioDecoder.Decode(file.Path, ffmpeg));

        Assert.Contains("ffmpeg", exception.Message);
    }

    [Fact]
    public void Decode_ПутьСНесуществующимФайлом_ВыбрасываетAudioDecodeException()
    {
        var ffmpeg = LocateFfmpeg();
        if (ffmpeg is null)
        {
            return;
        }

        Assert.Throws<AudioDecodeException>(() => FfmpegAudioDecoder.Decode(@"Z:\нет\такого.wav", ffmpeg));
    }
}