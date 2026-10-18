using System.Collections.Concurrent;
using System.Diagnostics;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Export;

/// <summary>Кодировщики видео, которые умеет запускать видеокарта. Реализация — <see cref="VideoEncoderDetector"/>.</summary>
public interface IVideoEncoderDetector
{
    /// <summary>
    /// Какие из известных производителей действительно кодируют на этой машине выбранный
    /// кодек. Пустой список - нормальный исход, а не ошибка: значит, кодировать придётся
    /// процессором.
    /// </summary>
    Task<VideoEncoderDetection> DetectAsync(string? ffmpegPath, string videoCodec, CancellationToken cancellationToken);
}

/// <summary>
/// Ответ пробы: что умеет видеокарта и запускается ли вообще ffmpeg.
/// </summary>
/// <param name="Supported">
/// Найденные производители. Пусто — кодировать придётся процессором.
/// </param>
/// <param name="FfmpegAvailable">
/// Запускается ли ffmpeg по указанному пути. Проверяется только при пустом списке: непустой
/// список уже доказал, что процесс запустился. Различать это приходится потому, что без
/// различения панель называла отсутствие ffmpeg отсутствием видеокарты.
/// </param>
public sealed record VideoEncoderDetection(IReadOnlyList<VideoEncoderSupport> Supported, bool FfmpegAvailable)
{
    /// <summary>Ничего не найдено, и сказать почему нечем.</summary>
    public static VideoEncoderDetection None { get; } = new([], FfmpegAvailable: false);
}

/// <summary>
/// Производитель видеокарты с кодировщиком ffmpeg, проверенным на этой машине.
/// </summary>
/// <param name="Vendor">Значение настройки: nvenc, qsv или amf.</param>
/// <param name="Title">Подпись для панели настроек.</param>
/// <param name="Encoder">Имя кодировщика ffmpeg выбранного кодека, например h264_nvenc.</param>
public sealed record VideoEncoderSupport(string Vendor, string Title, string Encoder);

/// <summary>
/// Ищет на машине кодировщики видеокарты: NVIDIA NVENC, Intel QSV и AMD AMF.
/// </summary>
/// <remarks>
/// Проверка в два шага, и второй важнее первого. Список кодировщиков в сборке ffmpeg ничего
/// не говорит о карте: h264_nvenc есть и на машине без NVIDIA, и падает при попытке им
/// закодировать кадр. Поэтому каждый найденный в списке кодировщик проверяется настоящим
/// кодированием одного кадра во временный поток, и в список попадают только те, кто это
/// сделал. Проба ничего не пишет на диск.
/// </remarks>
public sealed class VideoEncoderDetector : IVideoEncoderDetector
{
    /// <summary>Подписи производителей: ключ - значение настройки, оно же суффикс у ffmpeg.</summary>
    private static readonly Dictionary<string, string> VendorTitles = new()
    {
        ["nvenc"] = "NVIDIA",
        ["qsv"] = "Intel",
        ["amf"] = "AMD"
    };

    /// <summary>
    /// Известные производители. Порядок списка — это порядок выбора при «auto», и он же
    /// порядок суффиксов имён кодировщиков ffmpeg.
    /// </summary>
    public static IReadOnlyList<VideoVendor> Vendors { get; } =
        HardwareEncoderOptions.Suffixes
            .Select(suffix => new VideoVendor(suffix, VendorTitles[suffix]))
            .ToArray();

    /// <summary>Производитель видеокарты.</summary>
    /// <param name="Vendor">Значение настройки и суффикс имени кодировщика: nvenc, qsv или amf.</param>
    /// <param name="Title">Подпись для панели настроек.</param>
    public sealed record VideoVendor(string Vendor, string Title);

    /// <summary>Сколько ждём пробного кодирования одного кадра. Больше не ждём никогда.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Кэш разговора с ffmpeg: пара «путь + кодек» и ответ пробы. Панель настроек и выгрузка
    /// спрашивают одно и то же, а каждая проба — это запуск процесса.
    /// </summary>
    private readonly ConcurrentDictionary<(string Path, string Codec), VideoEncoderDetection> _cache = new();

    public async Task<VideoEncoderDetection> DetectAsync(
        string? ffmpegPath,
        string videoCodec,
        CancellationToken cancellationToken)
    {
        var codec = Family(videoCodec);

        if (codec is null)
        {
            return VideoEncoderDetection.None;
        }

        // Путь нормализуется один раз, и от нормализованного же считается ключ кэша. Раньше
        // пустая строка от панели и null от выгрузки попадали в кэш разными записями, хотя
        // означали одно и то же: «ищи в PATH».
        var executable = string.IsNullOrWhiteSpace(ffmpegPath) ? FfmpegLocator.ExecutableName : ffmpegPath;
        var key = (Path: executable, Codec: codec);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var found = await ProbeAsync(executable, codec, cancellationToken).ConfigureAwait(false);

        // Проверка «запускается ли ffmpeg» нужна только при пустом списке: непустой список
        // уже означает, что процесс отработал и вернул ноль.
        var detection = found.Count > 0 || await RunsAsync(executable, cancellationToken).ConfigureAwait(false)
            ? new VideoEncoderDetection(found, FfmpegAvailable: true)
            : VideoEncoderDetection.None;

        _cache[key] = detection;

        return detection;
    }

    /// <summary>
    /// Запускается ли ffmpeg по этому пути вообще. Отдельная проверка нужна, чтобы панель не
    /// говорила «видеокарты нет» там, где на самом деле нет ffmpeg.
    /// </summary>
    private static async Task<bool> RunsAsync(string executable, CancellationToken cancellationToken)
        => await RunsAsync(executable, VersionArguments(), cancellationToken).ConfigureAwait(false);

    private static IReadOnlyList<string> VersionArguments()
        => ["-hide_banner", "-version"];

    /// <summary>
    /// Запускает ffmpeg, который сам себе рисует один кадр и отдаёт его кодировщику в пустоту.
    /// Источник - встроенный фильтр lavfi, поэтому ни файла, ни звука не нужно.
    /// </summary>
    private static async Task<IReadOnlyList<VideoEncoderSupport>> ProbeAsync(
        string executable,
        string codec,
        CancellationToken cancellationToken)
    {
        var found = new List<VideoEncoderSupport>();

        foreach (var vendor in Vendors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var encoder = $"{codec}_{vendor.Vendor}";

            if (await RunsAsync(executable, ProbeArguments(encoder), cancellationToken).ConfigureAwait(false))
            {
                found.Add(new VideoEncoderSupport(vendor.Vendor, vendor.Title, encoder));
            }
        }

        return found;
    }

    /// <summary>
    /// Запускает ffmpeg с набором аргументов и отвечает, отработал ли он с нулевым кодом.
    /// Всё, что зонд делает с процессом, живёт здесь: и проба кодировщика, и проверка «ffmpeg
    /// вообще запускается».
    /// </summary>
    private static async Task<bool> RunsAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // stderr перенаправляется и вычитывается в никуда: у пробы нет файла на диске, куда
        // можно было бы его положить, а нечитаемый канал забился бы текстом и повис.
        // Причину неудачи пользователю показывать негде - её и негде и взять: об отсутствии
        // кодировщика сообщает сам список в панели, а об отсутствии ffmpeg - выгрузка.
        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.ErrorDataReceived += (_, _) => { };
            process.Start();
            process.BeginErrorReadLine();

            using var timeout = new CancellationTokenSource(ProbeTimeout);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            return process.ExitCode == 0;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Проба зависла намертво: у неё свой предел, и он не должен обрывать выгрузку.
            return false;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            // ffmpeg не запустился вовсе. Отсутствие кодировщика - не повод ронять панель:
            // выгрузка отдельно скажет, что ffmpeg не найден, и это понятнее внятного текста здесь.
            return false;
        }
    }

    /// <summary>
    /// Команда пробы. Отдельная чистая функция: её целиком проверяет тест, а сама проба
    /// запускает процесс и потому проверяется только на машине с нужной картой.
    /// </summary>
    /// <remarks>
    /// -f null отправляет кадр в пустоту, а не в файл, поэтому проба ничего не пишет.
    /// -nostdin обязателен: без него ffmpeg читает команды со стандартного ввода и может
    /// забрать байты у вызывающего процесса.
    /// </remarks>
    public static IReadOnlyList<string> ProbeArguments(string encoder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encoder);

        return
        [
            "-hide_banner",
            "-loglevel", "error",
            "-nostdin",

            // Источник рисует сам себе один кадр: ни файла, ни звука проба не читает.
            "-f", "lavfi",
            "-i", "color=c=black:s=128x128:r=1",
            "-frames:v", "1",
            "-c:v", encoder,
            "-f", "null", "-"
        ];
    }

    /// <summary>
    /// Семейство кодека у ffmpeg. У настроек кодек называется libx264 или libx265, а у
    /// кодировщика видеокарты — h264_nvenc или hevc_qsv; для сборки команды это одно и то же,
    /// и сверять их приходится именно так. Возвращает null, если имя не наше.
    /// </summary>
    public static string? Family(string? name)
    {
        var value = name?.Trim().ToLowerInvariant();

        if (value is null)
        {
            return null;
        }

        // Префикс сверяется со списком суффиксов, а не просто по началу имени: так h264_nvenc
        // узнаётся кодировщиком, а случайный h264_something не пройдёт.
        foreach (var suffix in HardwareEncoderOptions.Suffixes)
        {
            if (string.Equals(value, "h264_" + suffix, StringComparison.Ordinal))
            {
                return "h264";
            }

            if (string.Equals(value, "hevc_" + suffix, StringComparison.Ordinal))
            {
                return "hevc";
            }
        }

        return value switch
        {
            "libx264" => "h264",
            "libx265" => "hevc",
            _ => null
        };
    }
}
