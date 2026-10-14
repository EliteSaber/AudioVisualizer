namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Воспроизведение трека. Файл открывается отдельным треком: так звучит исходная стереодорожка,
/// а спектр считается по моно-миксу, который уже лежит в памяти.
/// </summary>
/// <remarks>
/// Плеер помнит позицию: файл можно открыть, перемотать ползунком и только потом нажать
/// «воспроизвести», и пауза продолжит с места, где её поставили. Трек и вывод создаются
/// фабриками, чтобы состояние плеера можно было проверить без звуковой карты.
/// </remarks>
public sealed class AudioPlayer : IAudioPlayer
{
    private readonly Func<string, IAudioTrack> _trackFactory;
    private readonly Func<IAudioOutput> _outputFactory;

    private IAudioTrack? _track;
    private IAudioOutput? _output;
    private string? _openedPath;
    private TimeSpan _position;

    public AudioPlayer(Func<string, IAudioTrack>? trackFactory = null, Func<IAudioOutput>? outputFactory = null)
    {
        _trackFactory = trackFactory ?? (path => new AudioFileTrack(path));
        _outputFactory = outputFactory ?? (() => new WaveOutOutput());
    }

    public bool IsPlaying => _output?.IsPlaying ?? false;

    public TimeSpan Position => _track?.Position ?? _position;

    public void Play(string path, double volume)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (_track is not null && _openedPath == path)
        {
            // Возобновление после паузы: трек уже открыт, переоткрывать его не нужно.
            _track.Volume = (float)Math.Clamp(volume, 0d, 1d);
            _output!.Play();
            return;
        }

        ReleaseDevices();
        OpenTrack(path, (float)Math.Clamp(volume, 0d, 1d), _position);
        _output!.Play();
    }

    public void Pause()
    {
        _output?.Pause();

        // Позицию запоминаем здесь: без неё возобновление начиналось бы с начала файла.
        _position = _track?.Position ?? _position;
    }

    public void StopPlayback()
    {
        _output?.StopPlayback();
        ReleaseDevices();
        _position = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position)
    {
        if (_track is null)
        {
            _position = position > TimeSpan.Zero ? position : TimeSpan.Zero;
            return;
        }

        var target = ClampToTrack(position);
        var wasPlaying = IsPlaying;

        if (!wasPlaying)
        {
            // На паузе звука нет, поэтому достаточно сдвинуть позицию: переоткрывать файл
            // ради этого не нужно.
            _track.Position = target;
            _position = target;

            return;
        }

        // На живом потоке позиция меняется на уже прочитанном буфере: вывод продолжает
        // отдавать звук, взятый до перемотки, и сдвиг слышно только в отчётной позиции.
        // Пересоздание одного устройства вывода проверялось и не помогло - помогает
        // только новый ридер, у которого буферы пусты.
        var path = _openedPath;
        var volume = _track.Volume;

        ReleaseDevices();

        if (path is null)
        {
            return;
        }

        OpenTrack(path, volume, target);
        _output!.Play();
    }

    public void Dispose()
    {
        StopPlayback();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Открывает трек с указанной позиции и создаёт под него устройство вывода. Позиция
    /// выставляется до инициализации вывода: иначе вывод успевает отдать первые кадры
    /// от начала файла.
    /// </summary>
    private void OpenTrack(string path, float volume, TimeSpan startAt)
    {
        _track = _trackFactory(path);
        _track.Volume = volume;

        if (startAt > TimeSpan.Zero)
        {
            _track.Position = ClampToTrack(startAt);
        }

        _output = _outputFactory();
        _output.Init(_track);
        _openedPath = path;
    }

    private TimeSpan ClampToTrack(TimeSpan position)
    {
        var total = _track?.Duration.TotalSeconds ?? position.TotalSeconds;

        return TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0d, Math.Max(0d, total)));
    }

    private void ReleaseDevices()
    {
        _output?.StopPlayback();
        _output?.Dispose();
        _output = null;
        _track?.Dispose();
        _track = null;

        // Путь сбрасывается вместе с треком: следующий Play другого файла не должен
        // принять его за уже открытый.
        _openedPath = null;
    }
}