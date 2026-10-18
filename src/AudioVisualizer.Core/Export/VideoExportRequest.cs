using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Export;

/// <summary>
/// Запрос на выгрузку видео.
/// </summary>
/// <param name="Audio">
/// Декодированный трек: из него берётся и спектр для кадров, и аудиодорожка mp4.
/// </param>
/// <param name="Settings">Настройки оформления и параметры экспорта. Нормализуются внутри экспортёра.</param>
/// <param name="OutputPath">Путь итогового mp4.</param>
public sealed record VideoExportRequest(AudioData Audio, AppSettings Settings, string OutputPath);

/// <summary>Ход выгрузки: сколько кадров готово, сколько всего и сколько осталось примерно.</summary>
/// <param name="FramesDone">Готовые кадры.</param>
/// <param name="TotalFrames">Всего кадров.</param>
/// <param name="Elapsed">Прошло времени с начала выгрузки.</param>
/// <param name="Remaining">Оценка оставшегося времени; до первого кадра неизвестна.</param>
public sealed record VideoExportProgress(int FramesDone, int TotalFrames, TimeSpan Elapsed, TimeSpan? Remaining)
{
    /// <summary>Доля готового от 0 до 1. Нулевой трек не считается делением на ноль.</summary>
    public double Fraction => TotalFrames <= 0 ? 0d : Math.Clamp(FramesDone / (double)TotalFrames, 0d, 1d);
}

/// <summary>
/// Итог выгрузки.
/// </summary>
/// <param name="Path">Путь записанного файла.</param>
/// <param name="Frames">Сколько кадров ушло в видео.</param>
/// <param name="Elapsed">Сколько заняла выгрузка.</param>
/// <param name="Cancelled">
/// Отменена ли выгрузка по требованию пользователя. Отмена - ожидаемый исход, а не ошибка,
/// поэтому она приходит таким же результатом, а не исключением: иначе отладчик и журналы
/// показывают красное исключение там, где всё штатно.
/// </param>
/// <param name="Encoder">
/// Чем на самом деле кодировалось видео. Пусто - процессором, иначе имя кодировщика
/// видеокарты, например h264_nvenc.
/// </param>
/// <param name="FellBackToCpu">
/// Просили кодировать видеокартой, а кодировала процессорная: карты не нашлось или она не
/// умеет выбранный кодек. Выгрузка от этого не хуже, но сказать о ней надо, иначе настройка
/// молча не сделала того, чего просили.
/// </param>
public sealed record VideoExportResult(
    string Path,
    int Frames,
    TimeSpan Elapsed,
    bool Cancelled = false,
    string? Encoder = null,
    bool FellBackToCpu = false);

/// <summary>
/// Выгрузка не удалась: ffmpeg не найден, вернул ошибку или не смог записать файл.
/// </summary>
/// <remarks>
/// В сообщении лежит всё для разбора: слова ffmpeg и сама команда. Журнала на диске нет -
/// он оставался в папке с роликами после каждой выгрузки, а из окна его не открыть.
/// </remarks>
public sealed class VideoExportException : Exception
{
    public VideoExportException(string message)
        : base(message)
    {
    }

    public VideoExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}