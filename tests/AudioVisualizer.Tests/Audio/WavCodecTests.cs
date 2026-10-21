using System.Text;
using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

public class WavCodecTests
{
    [Fact]
    public void WritePcm16_ИRead_СохраняютСэмплы()
    {
        float[] original = [0f, 0.5f, -0.25f, 1f, -1f];

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, original, 44100);

        stream.Position = 0;
        var result = WavCodec.Read(stream);

        Assert.Equal(44100, result.SampleRate);
        Assert.Equal(original.Length, result.Length);
        for (var i = 0; i < original.Length; i++)
        {
            Assert.InRange(result.Samples[i], original[i] - 0.0001f, original[i] + 0.0001f);
        }
    }

    [Fact]
    public void WritePcm16_ПишетКорректныйЗаголовок()
    {
        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, [0f, 0f, 0f, 0f], 8000);

        var bytes = stream.ToArray();
        var header = Encoding.ASCII.GetString(bytes, 0, 12);

        Assert.Equal("RIFF", header[..4]);
        Assert.Equal("WAVE", header[8..12]);
        Assert.Equal("fmt ", Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(8000, BitConverter.ToInt32(bytes, 24));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 22));
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
    }

    [Fact]
    public void WritePcm16_Стерео_ПишетЧислоКаналовВЗаголовке()
    {
        // Байт в секунду и размер кадра считаются по числу каналов: без этого ffmpeg
        // прочитает стерео-файл как сдвинутый на полкадра.
        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, [0.5f, -0.5f, 0.25f, -0.25f], 44100, 2);

        var bytes = stream.ToArray();

        Assert.Equal(2, BitConverter.ToInt16(bytes, 22));
        Assert.Equal(44100 * 2 * 2, BitConverter.ToInt32(bytes, 28));
        Assert.Equal(4, BitConverter.ToInt16(bytes, 32));
    }

    [Fact]
    public void WritePcm16_ИReadInterleaved_СохраняютКаналы()
    {
        float[] original = [0.5f, -0.5f, 0.25f, -0.25f, 0f, 0f];

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, original, 44100, 2);

        stream.Position = 0;
        var result = WavCodec.ReadInterleaved(stream);

        Assert.Equal(2, result.Channels);
        Assert.Equal(3, result.FrameCount);

        for (var i = 0; i < original.Length; i++)
        {
            Assert.InRange(result.Samples[i], original[i] - 0.0001f, original[i] + 0.0001f);
        }
    }

    [Fact]
    public void ReadInterleaved_МоноФайл_ДаётОдинКанал()
    {
        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, [0.5f, -0.25f], 44100);

        stream.Position = 0;
        var result = WavCodec.ReadInterleaved(stream);

        Assert.Equal(1, result.Channels);
        Assert.Equal(2, result.FrameCount);
    }

    [Fact]
    public void WritePcm16_НечисловыеСэмплыСтановятсяТихой()
    {
        // Math.Clamp на NaN пропускает значение насквозь, а (short)float.NaN в C# не
        // определено и даёт скачок полной громкости вместо тишины.
        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, [float.NaN, float.PositiveInfinity, float.NegativeInfinity], 44100);

        stream.Position = 0;
        var result = WavCodec.Read(stream);

        Assert.All(result.Samples, sample => Assert.InRange(sample, -0.0001f, 0.0001f));
    }

    [Fact]
    public void Read_НечисловыеСэмплыFloat32СтановятсяТихой()
    {
        // float-WAV от ffmpeg может содержать NaN, и сумма каналов с таким значением
        // испортила бы весь кадр вместе с соседями.
        var payload = new byte[16];
        BitConverter.GetBytes(float.NaN).CopyTo(payload, 0);
        BitConverter.GetBytes(0.5f).CopyTo(payload, 8);

        var content = RawWav.Build(format: 3, channels: 2, sampleRate: 44100, bitsPerSample: 32, payload);
        using var stream = new MemoryStream(content);

        var result = WavCodec.Read(stream);

        Assert.InRange(result.Samples[0], -0.0001f, 0.0001f);
    }

    [Fact]
    public void WritePcm16_РазмерВЗаголовкеСовпадаетСДанными()
    {
        // Разъявшаяся длина в заголовке обманывает ffmpeg на каждой выгрузке: он читает
        // ровно столько байт, сколько обещано, и ролик молча теряет конец трека.
        var samples = new float[10_000];

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, 44100);

        var bytes = stream.ToArray();

        Assert.Equal(44 + samples.Length * 2, bytes.Length);
        Assert.Equal(bytes.Length - 8, BitConverter.ToInt32(bytes, 4));
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(bytes, 40));
    }

    [Fact]
    public void WritePcm16_РазмерВЗаголовкеСовпадаетСДаннымиПриНеполномБлоке()
    {
        // Блок записи на 4096 сэмплов: неполный хвост дописывается после цикла, и его
        // длина тоже должна попасть в заголовок.
        var samples = new float[4097];

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, 44100);

        var bytes = stream.ToArray();

        Assert.Equal(44 + samples.Length * 2, bytes.Length);
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(bytes, 40));
    }

    [Fact]
    public void WritePcm16_ОбрезаетЗначенияВнеДиапазона()
    {
        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, [2f, -2f], 44100);

        stream.Position = 0;
        var result = WavCodec.Read(stream);

        Assert.InRange(result.Samples[0], 0.999f, 1f);
        Assert.InRange(result.Samples[1], -1f, -0.999f);
    }

    [Fact]
    public void Read_ЧитаетFloat32Wav()
    {
        var content = RawWav.Build(format: 3, channels: 1, sampleRate: 44100, bitsPerSample: 32, RawWav.Float32(0f, 0.25f, -0.5f));
        using var stream = new MemoryStream(content);

        var result = WavCodec.Read(stream);

        Assert.Equal(44100, result.SampleRate);
        Assert.Equal([0f, 0.25f, -0.5f], result.Samples);
    }

    [Fact]
    public void Read_СтереоWav_УсредняетКаналы()
    {
        // Левый 1.0, правый 0.0 - в моно должно попасть 0.5.
        var content = RawWav.Build(format: 1, channels: 2, sampleRate: 44100, bitsPerSample: 16, RawWav.Pcm16(32767, 0));
        using var stream = new MemoryStream(content);

        var result = WavCodec.Read(stream);

        Assert.Equal(1, result.Length);
        Assert.InRange(result.Samples[0], 0.49f, 0.51f);
    }

    [Fact]
    public void Read_ПропускаетБлокиПередДанными()
    {
        using var stream = new MemoryStream();
        stream.Write("RIFF"u8);
        stream.Write(new byte[4]);
        stream.Write("WAVE"u8);

        stream.Write("LIST"u8);
        stream.Write(BitConverter.GetBytes(2));
        stream.Write([1, 2]);

        var payload = RawWav.Pcm16(1000);
        stream.Write("fmt "u8);
        stream.Write(BitConverter.GetBytes(16));
        stream.Write(BitConverter.GetBytes((short)1));
        stream.Write(BitConverter.GetBytes((short)1));
        stream.Write(BitConverter.GetBytes(44100));
        stream.Write(BitConverter.GetBytes(88200));
        stream.Write(BitConverter.GetBytes((short)2));
        stream.Write(BitConverter.GetBytes((short)16));
        stream.Write("data"u8);
        stream.Write(BitConverter.GetBytes(payload.Length));
        stream.Write(payload);
        stream.Position = 0;

        var result = WavCodec.Read(stream);

        Assert.Equal(44100, result.SampleRate);
        Assert.InRange(result.Samples[0], 0.03f, 0.032f);
    }

    [Fact]
    public void Read_ОтклоняетФайлБезЗаголовкаRiff()
    {
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);

        Assert.Throws<InvalidDataException>(() => WavCodec.Read(stream));
    }

    [Fact]
    public void Read_ОтклоняетНеизвестноеКодирование()
    {
        var content = RawWav.Build(format: 7, channels: 1, sampleRate: 8000, bitsPerSample: 8, [0, 1, 2, 3]);
        using var stream = new MemoryStream(content);

        Assert.Throws<NotSupportedException>(() => WavCodec.Read(stream));
    }

    [Fact]
    public void Read_ОтклоняетWavБезБлокаДанных()
    {
        var content = RawWav.Build(format: 1, channels: 1, sampleRate: 8000, bitsPerSample: 16, []);
        using var stream = new MemoryStream(content);

        Assert.Throws<InvalidDataException>(() => WavCodec.Read(stream));
    }

    [Fact]
    public void WritePcm16_ОтклоняетНекорректнуюЧастоту()
    {
        using var stream = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => WavCodec.WritePcm16(stream, [0f], 0));
    }
}