using System.Buffers.Binary;
using System.Runtime.InteropServices;
using AudioVisualizer.Core.Settings;
using NAudio.Wave;

namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Источник звука с эквалайзером: сэмплы проходят через полосы и ограничитель пика,
/// и возвращаются в том же формате, в каком пришли.
/// </summary>
/// <remarks>
/// Байтовый уровень выбран потому, что <see cref="IAudioTrack"/> наследует
/// <see cref="IWaveProvider"/>, а вывод звуковой карты ждёт именно PCM. Цепочка
/// <c>ISampleProvider</c> означала бы переделку формата в том месте, где уже намучились
/// с перемоткой.
/// <para>
/// Формат берётся у источника, а не предполагается: <c>AudioFileReader</c> в NAudio 3
/// отдаёт IEEE float, и проверка «а вдруг это PCM» молча выключала бы обработку целиком.
/// </para>
/// </remarks>
public sealed class EqualizingWaveProvider : IWaveProvider
{
    /// <summary>Деление на 32768 при чтении и умножение на 32767 при записи: так же, как в WavCodec.</summary>
    private const float PcmScale = 32768f;

    private const float PcmWriteScale = 32767f;

    private readonly IWaveProvider _source;
    private readonly SampleFormat _format;
    private readonly int _channels;
    private readonly int _sampleRate;

    private PeakEqualizer? _equalizer;
    private PeakLimiter? _limiter;
    private float[] _buffer = [];

    public EqualizingWaveProvider(IWaveProvider source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _channels = source.WaveFormat.Channels;
        _sampleRate = source.WaveFormat.SampleRate;
        _format = DetectFormat(source.WaveFormat);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Задаёт полосы эквалайзера. Состояние фильтров сохраняется: полосы меняются
    /// ползунком на ходу, и пересборка цепочки была бы щелчком.
    /// </summary>
    public void SetEqualizer(IReadOnlyList<EqualizerBand> bands, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(bands);

        if (_format == SampleFormat.Unsupported || !enabled)
        {
            _equalizer = null;
            _limiter = null;
            return;
        }

        if (_equalizer is null)
        {
            _equalizer = new PeakEqualizer(_sampleRate, _channels, bands);
        }
        else
        {
            _equalizer.UpdateBands(bands);
        }

        _limiter ??= new PeakLimiter(_sampleRate);
    }

    public int Read(Span<byte> buffer)
    {
        // Плоские полосы не меняют звук, и переводить его тогда незачем: путь
        // байт-байт-байт слегка меняет сэмпл на единицу.
        if (_equalizer is null || !_equalizer.HasEffect)
        {
            return _source.Read(buffer);
        }

        var read = _source.Read(buffer);

        if (read <= 0)
        {
            return read;
        }

        switch (_format)
        {
            case SampleFormat.Float32:
                FilterFloats(MemoryMarshal.Cast<byte, float>(buffer[..(read & ~3)]));
                break;

            case SampleFormat.Pcm16:
                FilterPcm16(buffer[..(read & ~1)]);
                break;
        }

        return read;
    }

    /// <summary>Фильтрует сэмплы, уже лежащие во float: переводить нечего.</summary>
    private void FilterFloats(Span<float> samples)
    {
        _equalizer!.Apply(samples);

        // Ограничитель нужен только когда полосы подняты: при срезах вниз переполнения
        // не бывает, и включённый ограничитель тихо убавил бы громкость превью.
        if (_equalizer.HasBoost && _limiter is not null)
        {
            _limiter.Apply(samples, _channels);
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = Math.Clamp(samples[i], -1f, 1f);
        }
    }

    /// <summary>Фильтрует PCM 16 бит: байты переводятся в float и обратно.</summary>
    private void FilterPcm16(Span<byte> buffer)
    {
        var count = buffer.Length / 2;

        // Буфер переиспользуется между чтениями, и он может быть длиннее порции:
        // лишние сэмплы в обработку не попадают, иначе состояние фильтров считало бы
        // старые данные и вносило бы щелчок на стыке порций.
        var samples = EnsureBuffer(count).AsSpan(0, count);

        for (var i = 0; i < count; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(i * 2, 2)) / PcmScale;
        }

        _equalizer!.Apply(samples);

        if (_equalizer.HasBoost && _limiter is not null)
        {
            _limiter.Apply(samples, _channels);
        }

        for (var i = 0; i < count; i++)
        {
            var value = Math.Clamp(samples[i], -1f, 1f);
            BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(i * 2, 2), (short)MathF.Round(value * PcmWriteScale));
        }
    }

    private static SampleFormat DetectFormat(WaveFormat format)
    {
        if (format.Channels <= 0 || format.SampleRate <= 0)
        {
            return SampleFormat.Unsupported;
        }

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            return SampleFormat.Float32;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 16)
        {
            return SampleFormat.Pcm16;
        }

        return SampleFormat.Unsupported;
    }

    private float[] EnsureBuffer(int count)
    {
        if (_buffer.Length < count)
        {
            _buffer = new float[count];
        }

        return _buffer;
    }

    /// <summary>Формат сэмплов источника: во float, в PCM или ни то ни другое.</summary>
    private enum SampleFormat
    {
        Unsupported,
        Float32,
        Pcm16
    }
}
