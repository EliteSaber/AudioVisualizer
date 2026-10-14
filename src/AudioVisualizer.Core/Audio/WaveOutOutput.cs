using NAudio.Wave;

namespace AudioVisualizer.Core.Audio;

/// <summary>Вывод через звуковую карту: тонкая обёртка над <see cref="WaveOut"/>.</summary>
public sealed class WaveOutOutput : IAudioOutput
{
    private readonly WaveOut _output = new();

    public bool IsPlaying => _output.PlaybackState == PlaybackState.Playing;

    public void Init(IWaveProvider provider) => _output.Init(provider);

    public void Play() => _output.Play();

    public void Pause() => _output.Pause();

    public void StopPlayback() => _output.Stop();

    public void Dispose() => _output.Dispose();
}