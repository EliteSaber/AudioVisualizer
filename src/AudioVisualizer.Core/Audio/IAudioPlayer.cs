namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Воспроизведение трека. Интерфейс нужен, чтобы модель представления можно было проверять
/// тестами: настоящее воспроизведение требует звуковой карты и в тестах не работает.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    bool IsPlaying { get; }

    TimeSpan Position { get; }

    void Play(string path);

    /// <summary>
    /// Меняет громкость прослушивания. У открытого трека она меняется сразу, без
    /// переоткрытия файла; при последующем Play трек открывается с последней громкостью.
    /// </summary>
    void SetVolume(double volume);

    void Pause();

    /// <summary>Останавливает воспроизведение и возвращает позицию в начало.</summary>
    void StopPlayback();

    void Seek(TimeSpan position);
}