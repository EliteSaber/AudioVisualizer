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

    /// <summary>
    /// Тег дорожки H.265. Без него ffmpeg пишет hev1, а его не открывают QuickTime и часть
    /// плееров на телефонах: разница в том, где лежат служебные записи кодека, и читающий
    /// игнорирует их вместо нужных.
    /// </summary>
    private static readonly string[] HevcMp4Tag = ["-tag:v", "hvc1"];

    /// <param name="encoder">
    /// Кодировщик видеокарты (h264_nvenc и подобные). Пусто - кодируем процессором выбранным
    /// в настройках кодеком. Выбор кодировщика приходит из выгрузки, где видеокарта уже
    /// проверена пробным кадром: собирать команду раньше нечего.
    /// </param>
    public static IReadOnlyList<string> Build(
        ExportSettings settings,
        string audioPath,
        string outputPath,
        string? encoder = null)
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

        var videoCodec = settings.VideoCodec?.Trim().ToLowerInvariant();
        if (videoCodec is null || !ExportSettings.VideoCodecs.Contains(videoCodec, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Неизвестный кодек видео \"{settings.VideoCodec}\". Ожидается один из: {string.Join(", ", ExportSettings.VideoCodecs)}.",
                nameof(settings));
        }

        var (videoEncoder, quality) = ResolveEncoder(encoder, videoCodec, preset, crf);

        var coder = settings.AacCoder?.Trim().ToLowerInvariant();
        if (coder is null || !ExportSettings.AacCoders.Contains(coder, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Неизвестный кодировщик звука \"{settings.AacCoder}\". Ожидается один из: {string.Join(", ", ExportSettings.AacCoders)}.",
                nameof(settings));
        }

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

            // Кодек берётся из настроек, а у кодировщика видеокарты своя шкала CRF и пресета,
            // поэтому вместо -crf и нашего списка пресетов туда уходят переведённые значения.
            "-c:v", videoEncoder,
            .. quality,

            // yuv420p требует чётных размеров кадра, иначе ffmpeg падает на первом же кадре.
            "-pix_fmt", "yuv420p",

            "-c:a", "aac",

            // Кодировщик задаётся явно, хотя nmr и есть значение по умолчанию. Причина в том,
            // что выбор по умолчанию упирается в ffmpeg, а не в нас: с ним выгрузку приходится
            // прерывать. Явное значение делает команду одинаковой при любом значении по умолчанию
            // в чужой сборке. nmr стоит первым и лучше по качеству, twoloop и fast проверены
            // на треке, где nmr зависает; насколько надёжен каждый из них на других треках,
            // не проверено, поэтому список оставлен без своих пометок.
            "-aac_coder", coder,
            "-b:a", string.Create(CultureInfo.InvariantCulture, $"{bitrate}k"),
            "-movflags", "+faststart",

            // Видео беззвучно не заканчивается: без -shortest ffmpeg ждал бы конца потока.
            "-shortest",

            // Тег нужен по кодеку, а не по кодировщику: hvc1 ставят и libx265, и hevc_nvenc.
            .. VideoEncoderDetector.Family(videoCodec) == "hevc" ? HevcMp4Tag : [],
            outputPath
        ];
    }

    /// <summary>
    /// Кодировщик и его параметры качества. Кодировщик видеокарты обязан совпадать с кодеком
    /// из настроек: hevc_nvenc при libx264 упал бы на первом кадре, и разбираться пришлось бы
    /// по словам ffmpeg вместо внятного отказа здесь.
    /// </summary>
    private static (string Encoder, IReadOnlyList<string> Quality) ResolveEncoder(
        string? encoder,
        string videoCodec,
        string preset,
        int crf)
    {
        if (string.IsNullOrWhiteSpace(encoder))
        {
            return (videoCodec, ["-preset", preset, "-crf", crf.ToString(CultureInfo.InvariantCulture)]);
        }

        var suffix = HardwareEncoderOptions.SuffixOf(encoder);

        if (suffix is null)
        {
            throw new ArgumentException(
                $"Неизвестный кодировщик видеокарты \"{encoder}\". Ожидается один из: {string.Join(", ", HardwareEncoderOptions.Suffixes)}.",
                nameof(encoder));
        }

        var expected = VideoEncoderDetector.Family(videoCodec);

        if (!string.Equals(expected, VideoEncoderDetector.Family(encoder), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Кодировщик \"{encoder}\" не подходит кодеку \"{videoCodec}\": нужен {expected}_{suffix}.",
                nameof(encoder));
        }

        return (encoder, HardwareEncoderOptions.Quality(suffix, preset, crf));
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