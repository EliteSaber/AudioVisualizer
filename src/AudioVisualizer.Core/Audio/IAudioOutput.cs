using NAudio.Wave;

namespace AudioVisualizer.Core.Audio;

/// <summary>Устройство вывода звука. Отделяется от плеера ради тех же тестов.</summary>
public interface IAudioOutput : IDisposable
{
    bool IsPlaying { get; }

    void Init(IWaveProvider provider);

    void Play();

    void Pause();

    /// <summary>Имя не Stop: это зарезервированное слово в Visual Basic (CA1716).</summary>
    void StopPlayback();
}