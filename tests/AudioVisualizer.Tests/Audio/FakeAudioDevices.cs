using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;
using NAudio.Wave;

namespace AudioVisualizer.Tests.Audio;

/// <summary>
/// Подмены устройств для <see cref="AudioPlayer"/>. Настоящее воспроизведение требует звуковой
/// карты, а состояние плеера проверять нужно целиком: пауза, возобновление и перемотка живут
/// именно здесь.
/// </summary>
internal sealed class FakeAudioTrack : IAudioTrack
{
    public FakeAudioTrack(TimeSpan duration) => Duration = duration;

    public TimeSpan Position { get; set; }

    public TimeSpan Duration { get; }

    public float Volume { get; set; }

    public bool IsDisposed { get; private set; }

    public WaveFormat WaveFormat { get; } = new(44100, 16, 1);

    /// <summary>Какие полосы эквалайзера задавали треку и в каком порядке.</summary>
    public List<IReadOnlyList<EqualizerBand>> Equalizers { get; } = [];

    public bool EqualizerEnabled { get; private set; }

    public int Read(Span<byte> buffer) => 0;

    public void SetEqualizer(IReadOnlyList<EqualizerBand> bands, bool enabled)
    {
        Equalizers.Add(bands);
        EqualizerEnabled = enabled;
    }

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakeAudioOutput : IAudioOutput
{
    public bool IsPlaying { get; private set; }

    public int InitCount { get; private set; }

    public int PlayCount { get; private set; }

    public int PauseCount { get; private set; }

    public int StopCount { get; private set; }

    public IWaveProvider? Provider { get; private set; }

    public bool IsDisposed { get; private set; }

    public void Init(IWaveProvider provider)
    {
        InitCount++;
        Provider = provider;
    }

    public void Play()
    {
        PlayCount++;
        IsPlaying = true;
    }

    public void Pause()
    {
        PauseCount++;
        IsPlaying = false;
    }

    public void StopPlayback()
    {
        StopCount++;
        IsPlaying = false;
    }

    public void Dispose() => IsDisposed = true;
}