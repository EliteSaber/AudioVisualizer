using System.Buffers.Binary;
using System.Text;

namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Разбор и запись WAV. Через него проходят два потока: WAV от ffmpeg при декодировании и
/// временный PCM16 WAV для аудиодорожки mp4.
/// </summary>
public static class WavCodec
{
    private const int FormatPcm = 1;
    private const int FormatIeeeFloat = 3;
    private const int FormatExtensible = 0xFFFE;

    /// <summary>Сэмплов в одной записи в поток. Мелкие блоки сокращают число вызовов, но не память.</summary>
    private const int WriteBlockSamples = 4096;

    public static AudioData Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        if (ReadFourCc(reader) != "RIFF")
        {
            throw new InvalidDataException("Файл не является WAV: нет заголовка RIFF.");
        }

        // Между RIFF и WAVE лежит размер остатка файла; в потоке от ffmpeg он равен -1.
        _ = reader.ReadUInt32();

        if (ReadFourCc(reader) != "WAVE")
        {
            throw new InvalidDataException("Файл не является WAV: нет заголовка WAVE.");
        }

        var format = 0;
        var channels = 0;
        var sampleRate = 0;
        var bitsPerSample = 0;
        byte[]? payload = null;

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = ReadFourCc(reader);
            var chunkSize = reader.ReadInt32();

            // При записи в поток ffmpeg не знает длину заранее и пишет -1 вместо размера.
            var isSizeUnknown = chunkSize < 0;
            if (isSizeUnknown && chunkId != "data")
            {
                throw new InvalidDataException($"Блок \"{chunkId}\" в WAV имеет отрицательный размер {chunkSize}.");
            }

            var chunkStart = stream.Position;

            switch (chunkId)
            {
                case "fmt ":
                    format = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();

                    if (format == FormatExtensible && chunkSize >= 40)
                    {
                        reader.BaseStream.Position += 2 + 2 + 4;
                        format = reader.ReadInt16();
                    }

                    break;

                case "data":
                    var dataSize = isSizeUnknown ? (int)(stream.Length - chunkStart) : chunkSize;
                    payload = reader.ReadBytes(dataSize);
                    break;
            }

            stream.Position = isSizeUnknown
                ? stream.Length
                : Math.Min(chunkStart + chunkSize + (chunkSize % 2), stream.Length);
        }

        if (channels < 1 || sampleRate < 1 || payload is null)
        {
            throw new InvalidDataException("WAV не содержит блоков fmt и data нужного вида.");
        }

        var samples = Convert(payload, format, bitsPerSample, channels);
        if (samples.Length == 0)
        {
            throw new InvalidDataException("WAV не содержит ни одного сэмпла.");
        }

        return new AudioData(samples, sampleRate);
    }

    public static void WritePcm16(Stream stream, ReadOnlySpan<float> samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        var dataSize = samples.Length * 2;

        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)FormatPcm);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataSize);

        // Сэмплы пишутся блоками, а не по одному: BinaryWriter.Write(short) дергает поток на каждый
        // сэмпл, а на треке в четыре минуты это десятки миллионов вызовов, и на таком файле
        // приложение вставало на всё время записи.
        var block = new byte[WriteBlockSamples * 2];
        var filled = 0;

        foreach (var sample in samples)
        {
            var value = Math.Clamp(AudioData.Finite(sample), -1f, 1f);
            BinaryPrimitives.WriteInt16LittleEndian(block.AsSpan(filled), (short)MathF.Round(value * 32767f));
            filled += 2;

            if (filled == block.Length)
            {
                stream.Write(block, 0, filled);
                filled = 0;
            }
        }

        if (filled > 0)
        {
            stream.Write(block, 0, filled);
        }
    }

    public static void WritePcm16(string path, ReadOnlySpan<float> samples, int sampleRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.Create(path);
        WritePcm16(stream, samples, sampleRate);
    }

    private static float[] Convert(byte[] payload, int format, int bitsPerSample, int channels)
    {
        var isFloat = format is FormatIeeeFloat && bitsPerSample == 32;
        var isPcm16 = format is FormatPcm or FormatExtensible && bitsPerSample == 16;

        if (!isFloat && !isPcm16)
        {
            throw new NotSupportedException($"WAV с кодированием {format} и разрядностью {bitsPerSample} не поддерживается.");
        }

        var bytesPerSample = bitsPerSample / 8;
        var blockAlign = bytesPerSample * channels;
        var frameCount = payload.Length / blockAlign;

        var samples = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            var offset = frame * blockAlign;

            for (var channel = 0; channel < channels; channel++)
            {
                var index = offset + (channel * bytesPerSample);
                sum += isFloat
                    ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(index)))
                    : BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(index)) / 32768f;
            }

            // Нечисловые сэмплы в float-WAV возможны, а сумма каналов с таким значением портит весь
            // кадр: сумма остаётся нечисловой и уезжает дальше в столбики и в WAV для ffmpeg.
            samples[frame] = AudioData.Finite(sum / channels);
        }

        return samples;
    }

    private static string ReadFourCc(BinaryReader reader)
        => Encoding.ASCII.GetString(reader.ReadBytes(4));
}