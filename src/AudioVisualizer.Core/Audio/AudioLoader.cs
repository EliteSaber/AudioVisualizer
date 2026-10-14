using System.Runtime.InteropServices;

namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Точка входа для загрузки аудиофайла: сначала встроенный декодер NAudio, при его отказе -
/// ffmpeg. Если не сработало ни то, ни другое, ошибка объясняет обе причины.
/// </summary>
public sealed class AudioLoader(FfmpegLocator? locator = null)
{
    private readonly FfmpegLocator _locator = locator ?? new FfmpegLocator();

    public AudioData Load(string path, CancellationToken cancellationToken = default)
        => LoadAsync(path, cancellationToken).GetAwaiter().GetResult();

    public async Task<AudioData> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Аудиофайл не найден: {path}", path);
        }

        Exception? naudioFailure = null;
        try
        {
            return await Task.Run(() => NaudioAudioDecoder.Decode(path), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or COMException)
        {
            naudioFailure = exception;
        }

        var ffmpegPath = _locator.Locate();
        if (ffmpegPath is null)
        {
            throw new AudioDecodeException(
                "Встроенный декодер не поддерживает этот формат, а ffmpeg не найден. Укажите путь к ffmpeg.exe в настройках.",
                naudioFailure);
        }

        try
        {
            return await FfmpegAudioDecoder.DecodeAsync(path, ffmpegPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AudioDecodeException($"Не удалось декодировать файл средствами ffmpeg: {exception.Message}", exception);
        }
    }
}