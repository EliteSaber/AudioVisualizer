using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Аудиодорожка ролика: полосы эквалайзера и ограничение пика.
/// </summary>
public class ExportAudioTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void БезЭквалайзера_ВозвращаетсяТотЖеМассив()
    {
        var audio = new AudioData([0.5f, -0.5f], SampleRate);
        var spectrum = new SpectrumSettings { ApplyEqualizerToAudio = false };

        var result = ExportAudio.PrepareMono(audio, spectrum);

        // Копия без нужды стоила бы памяти: на треке в четыре минуты это 42 МБ.
        Assert.Same(audio.Samples, result);
    }

    [Fact]
    public void ПустыеПолосы_ВозвращаетсяТотЖеМассив()
    {
        var audio = new AudioData([0.5f, -0.5f], SampleRate);
        var spectrum = new SpectrumSettings
        {
            ApplyEqualizerToAudio = true,
            Equalizer = [new EqualizerBand(1000f, 0f)]
        };

        var result = ExportAudio.PrepareMono(audio, spectrum);

        Assert.Same(audio.Samples, result);
    }

    [Fact]
    public void Эквалайзер_МеняетГромкость_ИНеТрогаетИсходник()
    {
        var samples = Sine(1000f, 0.5f, SampleRate);
        var audio = new AudioData(samples, SampleRate);
        var spectrum = new SpectrumSettings
        {
            ApplyEqualizerToAudio = true,
            Equalizer = [new EqualizerBand(1000f, 6f)]
        };

        var result = ExportAudio.PrepareMono(audio, spectrum);

        // Исходный массив общий со спектром кадров: правка на месте применила бы
        // визуальный эквалайзер дважды, и картинка в ролике разошлась бы с экранной.
        Assert.NotSame(samples, result);
        Assert.Equal(0.5f, samples.Max(), 3);

        var ratio = Rms(result, SampleRate / 2) / Rms(samples, SampleRate / 2);

        Assert.InRange(ratio, 1.8d, 2.2d);
    }

    [Fact]
    public void ПикВышеПредела_Ограничивается()
    {
        var samples = Sine(1000f, 1.5f, SampleRate);
        var audio = new AudioData(samples, SampleRate);
        var spectrum = new SpectrumSettings
        {
            ApplyEqualizerToAudio = true,
            Equalizer = [new EqualizerBand(1000f, 6f)]
        };

        var result = ExportAudio.PrepareMono(audio, spectrum);

        var peak = result.Max(Math.Abs);

        Assert.InRange(peak, ExportAudio.PeakLimit - 0.001f, ExportAudio.PeakLimit + 0.001f);
    }

    [Fact]
    public void ЗвукСКаналами_ФильтруетсяНаМесте()
    {
        var samples = new float[SampleRate * 2];
        for (var i = 0; i < SampleRate; i++)
        {
            samples[i * 2] = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / SampleRate);
            samples[(i * 2) + 1] = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / SampleRate);
        }

        var audio = new ChannelAudio(samples, 2, SampleRate);
        var spectrum = new SpectrumSettings
        {
            ApplyEqualizerToAudio = true,
            Equalizer = [new EqualizerBand(1000f, 6f)]
        };

        ExportAudio.PrepareChannels(audio, spectrum);

        // Буфер принадлежит выгрузке, поэтому копии нет: громкость выросла на месте.
        var ratio = Rms(samples, SampleRate) / Rms(Sine(1000f, 0.5f, SampleRate), SampleRate / 2);

        Assert.InRange(ratio, 1.8d, 2.2d);
    }

    private static float[] Sine(float frequencyHz, float amplitude, int length)
    {
        var samples = new float[length];

        for (var i = 0; i < length; i++)
        {
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * frequencyHz * i / SampleRate);
        }

        return samples;
    }

    private static double Rms(ReadOnlySpan<float> samples, int start)
    {
        var sum = 0d;

        for (var i = start; i < samples.Length; i++)
        {
            sum += (double)samples[i] * samples[i];
        }

        return Math.Sqrt(sum / (samples.Length - start));
    }
}
