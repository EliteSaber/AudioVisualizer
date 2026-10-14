namespace AudioVisualizer.Core.Audio;

/// <summary>ffmpeg обязателен для форматов вне mp3 и wav, поэтому его отсутствие - отдельная понятная ошибка.</summary>
public sealed class FfmpegNotFoundException()
    : Exception("ffmpeg не найден. Укажите путь к ffmpeg.exe в настройках или добавьте его папку в PATH.");