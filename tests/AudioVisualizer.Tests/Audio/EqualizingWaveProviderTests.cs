using System.Buffers.Binary;
using System.Runtime.InteropServices;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;
using NAudio.Wave;

namespace AudioVisualizer.Tests.Audio;

/// <summary>
/// Эквалайзер в превью: сэмплы проходят через полосы, и громкость звука меняется так же,
/// как меняется форма спектра на экране.
/// </summary>
/// <remarks>
/// Формат источника - float32: так отдаёт звук <c>AudioFileReader</c> в NAudio 3. Проверка
/// на настоящем ридере стоит рядом: подставной источник с другим форматом проверял бы
/// путь, которого в приложении нет, и поломка в формате прошла бы незамеченной.
/// </remarks>
public class EqualizingWaveProviderTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void НастоящийРидер_ОтдаётFloat_ИЭквалайзерРаботает()
    {
        using var file = StereoWavFile();

        using var reader = new AudioFileReader(file.Path);

        // Ридер в NAudio 3 отдаёт IEEE float, и обработка целиком выключается, если
        // формат не тот. Проверка падает с внятным текстом, а не молча.
        Assert.Equal(WaveFormatEncoding.IeeeFloat, reader.WaveFormat.Encoding);
        Assert.Equal(32, reader.WaveFormat.BitsPerSample);

        var provider = new EqualizingWaveProvider(reader);
        provider.SetEqualizer([new EqualizerBand(1000f, 6f)], enabled: true);

        var buffer = new byte[8192];
        var read = provider.Read(buffer);

        var output = Floats(buffer.AsSpan(0, read));
        var left = TestTone.Channel(output, 0);

        // 10^(6/20) = 1.995: во столько раз должна вырасти среднеквадратичное отклонение.
        var ratio = TestTone.Rms(left, 1000) / TestTone.Rms(TestTone.Channel(Original(), 0), 1000);

        Assert.InRange(ratio, 1.8d, 2.2d);
    }

    [Fact]
    public void БезЭквалайзера_БайтыНеМеняются()
    {
        var bytes = FloatBytes(TestTone.Sine(SampleRate, 1000f, 0.5f, 4096));
        var provider = new EqualizingWaveProvider(new FixedWaveProvider(SampleRate, 1, bytes));
        provider.SetEqualizer([new EqualizerBand(1000f, 6f)], enabled: false);

        var buffer = new byte[bytes.Length];
        provider.Read(buffer);

        Assert.Equal(bytes, buffer);
    }

    [Fact]
    public void ПлоскиеПолосы_БайтыНеМеняются()
    {
        // Включённый, но плоский эквалайзер не должен трогать звук: путь через
        // преобразования меняет сэмпл на единицу, и это слышно в тихих местах.
        var bytes = FloatBytes(TestTone.Sine(SampleRate, 1000f, 0.5f, 4096));
        var provider = new EqualizingWaveProvider(new FixedWaveProvider(SampleRate, 1, bytes));
        provider.SetEqualizer([new EqualizerBand(1000f, 0f)], enabled: true);

        var buffer = new byte[bytes.Length];
        provider.Read(buffer);

        Assert.Equal(bytes, buffer);
    }

    [Fact]
    public void ВключённыйЭквалайзер_ДаётПриростГромкости()
    {
        var input = TestTone.Sine(SampleRate, 1000f, 0.5f, SampleRate);
        var provider = new EqualizingWaveProvider(new FixedWaveProvider(SampleRate, 1, FloatBytes(input)));
        provider.SetEqualizer([new EqualizerBand(1000f, 6f)], enabled: true);

        var buffer = new byte[input.Length * 4];
        provider.Read(buffer);

        var output = Floats(buffer);

        // 10^(6/20) = 1.995: во столько раз должна вырасти среднеквадратичное отклонение.
        var ratio = TestTone.Rms(output, SampleRate / 2) / TestTone.Rms(input, SampleRate / 2);

        Assert.InRange(ratio, 1.8d, 2.2d);
    }

    [Fact]
    public void Стерео_ФильтруетсяПоКаналам()
    {
        // Слева синус 1 кГц, справа - 5 кГц. Полоса поднята на 1 кГц: левый канал вырастает,
        // правый остаётся прежним.
        var input = TestTone.Stereo(SampleRate, 1000f, 5000f, 0.5f, SampleRate);
        var provider = new EqualizingWaveProvider(new FixedWaveProvider(SampleRate, 2, FloatBytes(input)));
        provider.SetEqualizer([new EqualizerBand(1000f, 6f)], enabled: true);

        var buffer = new byte[input.Length * 4];
        provider.Read(buffer);

        var output = Floats(buffer);
        var left = TestTone.Channel(output, 0);
        var right = TestTone.Channel(output, 1);

        var leftRatio = TestTone.Rms(left, SampleRate / 2) / TestTone.Rms(TestTone.Channel(input, 0), SampleRate / 2);
        var rightRatio = TestTone.Rms(right, SampleRate / 2) / TestTone.Rms(TestTone.Channel(input, 1), SampleRate / 2);

        Assert.InRange(leftRatio, 1.8d, 2.2d);
        Assert.InRange(rightRatio, 0.95d, 1.05d);
    }

    [Fact]
    public void Pcm16ТожеФильтруется()
    {
        // Источник может отдавать и PCM: тогда сэмплы переводятся в float и обратно.
        var input = TestTone.Sine(SampleRate, 1000f, 0.5f, SampleRate);
        var provider = new EqualizingWaveProvider(new PcmWaveProvider(SampleRate, 1, input));
        provider.SetEqualizer([new EqualizerBand(1000f, 6f)], enabled: true);

        var buffer = new byte[input.Length * 2];
        provider.Read(buffer);

        var output = new float[buffer.Length / 2];
        for (var i = 0; i < output.Length; i++)
        {
            output[i] = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i * 2, 2)) / 32768f;
        }

        var ratio = TestTone.Rms(output, SampleRate / 2) / TestTone.Rms(input, SampleRate / 2);

        Assert.InRange(ratio, 1.8d, 2.2d);
    }

    [Fact]
    public void УсиленнаяПолоса_НеПереполняетВыход()
    {
        // Превью играет в реальном времени, и переполнение там слышно как треск.
        var input = TestTone.Sine(SampleRate, 1000f, 0.9f, SampleRate);
        var provider = new EqualizingWaveProvider(new FixedWaveProvider(SampleRate, 1, FloatBytes(input)));
        provider.SetEqualizer([new EqualizerBand(1000f, 12f)], enabled: true);

        var buffer = new byte[input.Length * 4];
        provider.Read(buffer);

        var output = Floats(buffer);

        Assert.All(output, sample => Assert.InRange(sample, -1.0001f, 1.0001f));
    }

    [Fact]
    public void СменаПолос_НеОбрываетЗвук()
    {
        // Полосы меняются ползунком на ходу. Обрыв был бы слышен щелчком, и его ловит
        // проверка разрыва между соседними сэмплами.
        var input = TestTone.Sine(SampleRate, 1000f, 0.5f, SampleRate);
        var provider = new EqualizingWaveProvider(new FixedWaveProvider(SampleRate, 1, FloatBytes(input)));
        provider.SetEqualizer([new EqualizerBand(1000f, 6f)], enabled: true);

        var first = new byte[8192];
        provider.Read(first);

        provider.SetEqualizer([new EqualizerBand(1000f, 12f)], enabled: true);

        var second = new byte[8192];
        provider.Read(second);

        var samples = Floats(second);
        var biggest = 0f;

        for (var i = 1; i < samples.Length; i++)
        {
            biggest = Math.Max(biggest, Math.Abs(samples[i] - samples[i - 1]));
        }

        // Разрыв в полсэмпла означал бы щелчок: сигнал на 1 кГц меняется за это время
        // на долю процента.
        Assert.True(biggest < 0.2f, $"Звук оборвался при смене полос: скачок {biggest}.");
    }

    [Fact]
    public void ФорматНеПоддерживается_ЗвукИдётБезОбработки()
    {
        // Фильтры считают во float, а не в каждом формате это возможно. Тогда звук идёт
        // как есть, а не роняет воспроизведение.
        var bytes = FloatBytes(TestTone.Sine(SampleRate, 1000f, 0.5f, 1024));
        var source = new FixedWaveProvider(SampleRate, 1, bytes)
        {
            WaveFormat = new WaveFormat(SampleRate, 24, 1)
        };
        var provider = new EqualizingWaveProvider(source);
        provider.SetEqualizer([new EqualizerBand(1000f, 6f)], enabled: true);

        var buffer = new byte[bytes.Length];
        provider.Read(buffer);

        Assert.Equal(bytes, buffer);
    }

    /// <summary>Стерео-WAV для настоящего ридера: слева синус, справа - противофазный.</summary>
    private static TemporaryAudioFile StereoWavFile()
    {
        var frames = SampleRate;
        var samples = new float[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            var value = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / SampleRate);
            samples[i * 2] = value;
            samples[(i * 2) + 1] = -value;
        }

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, SampleRate, 2);

        return new TemporaryAudioFile(stream.ToArray());
    }

    /// <summary>Тот же сигнал, что во временном файле: эталон для сравнения громкости.</summary>
    private static float[] Original()
    {
        var frames = SampleRate;
        var samples = new float[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            var value = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / SampleRate);
            samples[i * 2] = value;
            samples[(i * 2) + 1] = -value;
        }

        return samples;
    }

    /// <summary>Сэмплы в байты float32: так их отдаёт ридер NAudio 3.</summary>
    private static byte[] FloatBytes(float[] samples)
    {
        var bytes = new byte[samples.Length * 4];

        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), BitConverter.SingleToInt32Bits(samples[i]));
        }

        return bytes;
    }

    /// <summary>Байты float32 обратно в сэмплы.</summary>
    private static float[] Floats(ReadOnlySpan<byte> bytes)
    {
        var samples = new float[bytes.Length / 4];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i * 4, 4)));
        }

        return samples;
    }

    /// <summary>Источник с неизменным куском звука: тесту важно, что читается, а не откуда.</summary>
    private sealed class FixedWaveProvider(int sampleRate, int channels, byte[] bytes) : IWaveProvider
    {
        public WaveFormat WaveFormat { get; set; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        public int Read(Span<byte> buffer)
        {
            var count = Math.Min(buffer.Length, bytes.Length);
            bytes.AsSpan(0, count).CopyTo(buffer);

            return count;
        }
    }

    /// <summary>Источник с PCM 16 бит: другой формат на входе.</summary>
    private sealed class PcmWaveProvider(int sampleRate, int channels, float[] samples) : IWaveProvider
    {
        private readonly byte[] _bytes = PcmBytes(samples);

        public WaveFormat WaveFormat { get; } = new(sampleRate, 16, channels);

        public int Read(Span<byte> buffer)
        {
            var count = Math.Min(buffer.Length, _bytes.Length);
            _bytes.AsSpan(0, count).CopyTo(buffer);

            return count;
        }

        private static byte[] PcmBytes(float[] samples)
        {
            var bytes = new byte[samples.Length * 2];

            for (var i = 0; i < samples.Length; i++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), (short)MathF.Round(Math.Clamp(samples[i], -1f, 1f) * 32767f));
            }

            return bytes;
        }
    }
}
