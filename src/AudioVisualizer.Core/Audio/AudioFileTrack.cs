using NAudio.Wave;

namespace AudioVisualizer.Core.Audio;

/// <summary>Трек, открытый из файла: тонкая обёртка над <see cref="AudioFileReader"/>.</summary>
public sealed class AudioFileTrack : IAudioTrack
{
    private readonly AudioFileReader _reader;

    public AudioFileTrack(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _reader = new AudioFileReader(path);
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

    public WaveFormat WaveFormat => _reader.WaveFormat;

    public int Read(Span<byte> buffer) => _reader.Read(buffer);

    public void Dispose() => _reader.Dispose();
}