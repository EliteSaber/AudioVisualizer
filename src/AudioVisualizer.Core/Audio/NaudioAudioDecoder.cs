using NAudio.Wave;

namespace AudioVisualizer.Core.Audio;

/// <summary>Декодирование средствами NAudio: mp3, wav, aiff и всё, что умеет Media Foundation.</summary>
internal static class NaudioAudioDecoder
{
    private const int ReadBufferFrames = 4096;

    public static AudioData Decode(string path)
    {
        using var reader = new AudioFileReader(path);
        return ReadMono(reader.ToSampleProvider());
    }

    /// <summary>
    /// Декодирование с сохранением каналов: сэмплы идут чересполосно.
    /// </summary>
    /// <remarks>
    /// Моно-микс годится для спектра, а звук ролика должен быть как в исходнике: свод
    /// каналов теряет 3 дБ и местами вычитает фазу.
    /// </remarks>
    public static ChannelAudio DecodeChannels(string path)
    {
        using var reader = new AudioFileReader(path);
        return ReadChannels(reader.ToSampleProvider());
    }

    private static ChannelAudio ReadChannels(ISampleProvider provider)
    {
        var format = provider.WaveFormat;
        var channels = format.Channels;
        var samples = Array.Empty<float>();
        var buffer = new float[ReadBufferFrames * channels];
        var offset = 0;

        int read;
        while ((read = provider.Read(buffer.AsSpan())) > 0)
        {
            EnsureCapacity(ref samples, offset + read);
            Array.Copy(buffer, 0, samples, offset, read);
            offset += read;
        }

        return new ChannelAudio(
            offset == samples.Length ? samples : samples[..offset],
            channels,
            format.SampleRate);
    }

    private static AudioData ReadMono(ISampleProvider provider)
    {
        var format = provider.WaveFormat;
        var channels = format.Channels;

        // Для файловых ридеров это число кадров всего трека, для потоков - 0 и массив растёт по мере чтения.
        var estimatedFrames = format.AverageBytesPerSecond / format.BlockAlign;
        var samples = estimatedFrames > 0 ? new float[(int)Math.Min(estimatedFrames, int.MaxValue)] : Array.Empty<float>();

        var buffer = new float[ReadBufferFrames * channels];
        var offset = 0;

        int read;
        while ((read = provider.Read(buffer.AsSpan())) > 0)
        {
            var frameCount = read / channels;
            EnsureCapacity(ref samples, offset + frameCount);

            for (var frame = 0; frame < frameCount; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += buffer[(frame * channels) + channel];
                }

                // Нечисловой сэмпл декодера иначе испортил бы весь кадр: сумма с таким значением
                // остаётся нечисловой и уезжает в столбики спектра и в WAV для ffmpeg.
                samples[offset++] = AudioData.Finite(sum / channels);
            }
        }

        var result = offset == samples.Length ? samples : samples[..offset];
        return new AudioData(result, provider.WaveFormat.SampleRate);
    }

    private static void EnsureCapacity(ref float[] samples, int required)
    {
        if (samples.Length >= required)
        {
            return;
        }

        Array.Resize(ref samples, Math.Max(required, samples.Length == 0 ? 44100 : samples.Length * 2));
    }
}