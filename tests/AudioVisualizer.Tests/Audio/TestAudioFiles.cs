using System.Text;

namespace AudioVisualizer.Tests.Audio;

/// <summary>Сборка WAV вручную: нужны нестандартные заголовки, которые WavCodec писать не умеет.</summary>
internal static class RawWav
{
    public static byte[] Build(int format, int channels, int sampleRate, int bitsPerSample, byte[] payload)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write("RIFF"u8);
        writer.Write(36 + payload.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)format);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(payload.Length);
        writer.Write(payload);
        writer.Flush();

        return stream.ToArray();
    }

    public static byte[] Pcm16(params short[] samples)
    {
        var payload = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            payload[i * 2] = (byte)(samples[i] & 0xFF);
            payload[(i * 2) + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }

        return payload;
    }

    public static byte[] Float32(params float[] samples)
    {
        var payload = new byte[samples.Length * 4];
        for (var i = 0; i < samples.Length; i++)
        {
            var bits = BitConverter.SingleToInt32Bits(samples[i]);
            payload[i * 4] = (byte)(bits & 0xFF);
            payload[(i * 4) + 1] = (byte)((bits >> 8) & 0xFF);
            payload[(i * 4) + 2] = (byte)((bits >> 16) & 0xFF);
            payload[(i * 4) + 3] = (byte)((bits >> 24) & 0xFF);
        }

        return payload;
    }
}

/// <summary>Временный файл с автоудалением: тесты пишут реальные файлы на диск.</summary>
internal sealed class TemporaryAudioFile : IDisposable
{
    public TemporaryAudioFile(byte[] content, string extension = ".wav")
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"av-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(Path, content);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}