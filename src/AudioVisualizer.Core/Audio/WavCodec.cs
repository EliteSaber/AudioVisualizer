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
        var channels = ReadInterleaved(stream);

        return new AudioData(channels.ToMonoMix(), channels.SampleRate);
    }

    /// <summary>
    /// Разбор WAV с сохранением каналов: сэмплы идут чересполосно.
    /// </summary>
    /// <remarks>
    /// Моно-микс получается сводом каналов, и звук ролика без такого свода не совпал бы
    /// с исходником: свод теряет 3 дБ на разнесённых дорожках.
    /// </remarks>
    public static ChannelAudio ReadInterleaved(Stream stream)
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

        return new ChannelAudio(samples, channels, sampleRate);
    }

    public static void WritePcm16(Stream stream, ReadOnlySpan<float> samples, int sampleRate, int channels = 1)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), channels, "Число каналов должно быть положительным.");
        }

        if (samples.Length % channels != 0)
        {
            throw new ArgumentException(
                $"Число сэмплов ({samples.Length}) не делится на число каналов ({channels}).",
                nameof(samples));
        }

        var dataSize = samples.Length * 2;

        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)FormatPcm);
        writer.Write((short)channels);
        writer.Write(sampleRate);

        // Байт в секунду и размер кадра считаются по числу каналов: без этого ffmpeg
        // прочитает стерео-файл как сдвинутый на полкадра.
        writer.Write(sampleRate * 2 * channels);
        writer.Write((short)(2 * channels));
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

    public static void WritePcm16(string path, ReadOnlySpan<float> samples, int sampleRate, int channels = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.Create(path);
        WritePcm16(stream, samples, sampleRate, channels);
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

        // Сэмплы идут чересполосно: кадр занимает по одному сэмплу на канал.
        var samples = new float[frameCount * channels];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var offset = frame * blockAlign;

            for (var channel = 0; channel < channels; channel++)
            {
                var index = offset + (channel * bytesPerSample);

                // Нечисловой сэмпл декодера иначе испортил бы весь кадр: сумма каналов с таким
                // значением остаётся нечисловой и уезжает дальше в столбики и в WAV для ffmpeg.
                var value = isFloat
                    ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(index)))
                    : BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(index)) / 32768f;

                samples[(frame * channels) + channel] = AudioData.Finite(value);
            }
        }

        return samples;
    }

    private static string ReadFourCc(BinaryReader reader)
        => Encoding.ASCII.GetString(reader.ReadBytes(4));
}