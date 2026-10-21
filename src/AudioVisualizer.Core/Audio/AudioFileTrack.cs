using AudioVisualizer.Core.Settings;
using NAudio.Wave;

namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Трек, открытый из файла: тонкая обёртка над <see cref="AudioFileReader"/>.
/// </summary>
/// <remarks>
/// Звук проходит через <see cref="EqualizingWaveProvider"/>, поэтому полосы эквалайзера
/// слышны в превью, а файл при этом остаётся стерео.
/// </remarks>
public sealed class AudioFileTrack : IAudioTrack
{
    private readonly AudioFileReader _reader;
    private readonly EqualizingWaveProvider _chain;

    public AudioFileTrack(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _reader = new AudioFileReader(path);
        _chain = new EqualizingWaveProvider(_reader);
    }

    public TimeSpan Position
    {
        get => _reader.CurrentTime;
        set => _reader.CurrentTime = value;
    }

    public TimeSpan Duration => _reader.TotalTime;

    public float Volume
    {
        get => _reader.Volume;
        set => _reader.Volume = value;
    }

    public WaveFormat WaveFormat => _chain.WaveFormat;

    public int Read(Span<byte> buffer) => _chain.Read(buffer);

    public void SetEqualizer(IReadOnlyList<EqualizerBand> bands, bool enabled)
        => _chain.SetEqualizer(bands, enabled);

    public void Dispose() => _reader.Dispose();
}
