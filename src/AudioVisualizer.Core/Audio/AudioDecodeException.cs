namespace AudioVisualizer.Core.Audio;

/// <summary>Файл не удалось декодировать ни встроенным декодером, ни ffmpeg.</summary>
public sealed class AudioDecodeException(string message, Exception? innerException = null)
    : Exception(message, innerException);