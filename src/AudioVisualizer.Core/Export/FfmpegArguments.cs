using System.Globalization;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Export;

/// <summary>
/// Командная строка ffmpeg для выгрузки видео.
/// </summary>
/// <remarks>
/// Собирается отдельно от самого экспорта, потому что это чистая функция: её целиком
/// проверяет тест, а в процессе видно, что именно запускается. Видео идёт непрерывным
/// потоком кадров в stdin, поэтому параметры входа повторяют параметры вывода.
/// </remarks>
public static class FfmpegArguments
{
    private static readonly string[] CodecPresets =
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"
    ];

    public static IReadOnlyList<string> Build(ExportSettings settings, string audioPath, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var width = RequireEven(settings.Width, nameof(settings.Width));
        var height = RequireEven(settings.Height, nameof(settings.Height));

        if (settings.FramesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings), settings.FramesPerSecond, "Частота кадров должна быть положительной.");
        }

        var preset = settings.CodecPreset?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!CodecPresets.Contains(preset, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Неизвестный пресет кодека \"{settings.CodecPreset}\". Ожидается один из: {string.Join(", ", CodecPresets)}.",
                nameof(settings));
        }

        var crf = Math.Clamp(settings.Crf, 0, 51);
        var bitrate = Math.Clamp(settings.AudioBitrateKbps, 32, 320);

        return
        [
            "-y",

            // Баннер и прогресс на stderr не нужны: stderr читается как текст ошибки,
            // и полезные строки там не потеряются, а мусора будет меньше.
            "-hide_banner",
            "-loglevel", "error",

            // stdin занят кадрами, читать команды оттуда ffmpeg не должен.
            "-f", "rawvideo",
            "-pixel_format", "bgra",
            "-video_size", $"{width}x{height}",
            "-framerate", settings.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
            "-i", "pipe:0",

            "-i", audioPath,

            "-map", "0:v",
            "-map", "1:a",
            "-c:v", "libx264",
            "-preset", preset,
            "-crf", crf.ToString(CultureInfo.InvariantCulture),

            // yuv420p требует чётных размеров кадра, иначе ffmpeg падает на первом же кадре.
            "-pix_fmt", "yuv420p",

            "-c:a", "aac",
            "-b:a", string.Create(CultureInfo.InvariantCulture, $"{bitrate}k"),
            "-movflags", "+faststart",

            // Видео беззвучно не заканчивается: без -shortest ffmpeg ждал бы конца потока.
            "-shortest",
            outputPath
        ];
    }

    private static int RequireEven(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Размер кадра должен быть положительным.");
        }

        if (value % 2 != 0)
        {
            throw new ArgumentException($"Размер кадра {value} должен быть чётным.", parameterName);
        }

        return value;
    }
}