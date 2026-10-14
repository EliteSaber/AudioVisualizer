using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

/// <summary>
/// Подмена воспроизведения: настоящее требует звуковой карты, а модели представления
/// нужно знать только, что и в каком порядке вызывали. Ведёт себя как <see cref="AudioPlayer"/>,
/// включая запоминание позиции до запуска.
/// </summary>
internal sealed class FakeAudioPlayer : IAudioPlayer
{
    public bool IsPlaying { get; private set; }

    public TimeSpan Position { get; private set; }

    public int PlayCount { get; private set; }

    public int PauseCount { get; private set; }

    public int StopCount { get; private set; }

    public string? PlayedPath { get; private set; }

    public List<TimeSpan> Seeks { get; } = [];

    public Exception? PlayFailure { get; set; }

    public void Play(string path, double volume)
    {
        PlayCount++;

        if (PlayFailure is not null)
        {
            throw PlayFailure;
        }

        PlayedPath = path;
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
        Position = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position)
    {
        Seeks.Add(position);
        Position = position > TimeSpan.Zero ? position : TimeSpan.Zero;
    }

    /// <summary>Прокручивает время воспроизведения: так тест делает вид, что трек играет.</summary>
    public void AdvanceTo(TimeSpan position) => Position = position;

    public void Dispose()
    {
    }
}