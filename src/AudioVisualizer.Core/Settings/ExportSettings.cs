namespace AudioVisualizer.Core.Settings;

/// <summary>Параметры выгрузки видео.</summary>
public sealed record ExportSettings
{
    /// <summary>
    /// Кодировщики звука, которые умеет нативный кодировщик AAC из ffmpeg. Список один
    /// на всех: его читают и сборка команды, и нормализация, и панель настроек, чтобы
    /// значения не разъехались между тремя местами.
    /// </summary>
    public static IReadOnlyList<string> AacCoders { get; } = ["nmr", "twoloop", "fast"];

    /// <summary>
    /// Кодировщики видео. Список один на всех — по той же причине, что и у звука. Оба
    /// понимают одни и те же -preset и -crf, поэтому параметры качества общие.
    /// </summary>
    public static IReadOnlyList<string> VideoCodecs { get; } = ["libx264", "libx265"];

    /// <summary>
    /// Кодировщик видео по умолчанию. H.264 выбран не только по совместимости: файл с
    /// настройками, где этого поля ещё нет, должен читаться как раньше.
    /// </summary>
    public const string DefaultVideoCodec = "libx264";

    /// <summary>
    /// Кодировщик звука AAC. Значение по умолчанию - то, что выбирает сам ffmpeg, потому
    /// что оно даёт лучшее качество. Проверено, что на части сигналов он зависает, и в этом
    /// случае выгрузка прерывается с подсказкой сменить кодировщик.
    /// </summary>
    public const string DefaultAacCoder = "nmr";

    /// <summary>
    /// Чем кодируется видео. «software» - сам процессор, «auto» - видеокарта, если её нашлось,
    /// остальное - конкретный производитель. Значения кодировщиков ffmpeg здесь намеренно
    /// нет: пара «кодек + производитель» собирается в имя h264_nvenc уже при выгрузке, где
    /// известно, что видеокарта действительно есть.
    /// </summary>
    public static IReadOnlyList<string> VideoEncoders { get; } = ["software", "auto", "nvenc", "qsv", "amf"];

    /// <summary>
    /// Кодирование процессором по умолчанию. Видеокарта включается вручную: у неё своя шкала
    /// качества, и молчаливый переход на неё изменил бы картинку у всех, кто ничего не просил.
    /// </summary>
    public const string DefaultVideoEncoder = "software";

    /// <summary>Кодирование процессором, без видеокарты.</summary>
    public const string SoftwareVideoEncoder = "software";

    /// <summary>Кодирование видеокартой, если она нашлась и кодирует выбранный кодек.</summary>
    public const string AutoVideoEncoder = "auto";

    public int Width { get; init; } = 1920;

    public int Height { get; init; } = 1080;

    public int FramesPerSecond { get; init; } = 60;

    /// <summary>
    /// Кодек видео: libx264 или libx265. Список - в <see cref="VideoCodecs"/>. В панели
    /// настроек показывается как H.264 и H.265, имена ffmpeg остаются в файле и в команде.
    /// </summary>
    public string VideoCodec { get; init; } = DefaultVideoCodec;

    /// <summary>Качество видео: меньше - лучше и медленнее. 18 - почти без потерь.</summary>
    public int Crf { get; init; } = 18;

    /// <summary>
    /// Чем кодируется видео: процессором, видеокартой по выбору или по автомату. Список -
    /// в <see cref="VideoEncoders"/>. Настоящие имена кодировщиков ffmpeg появляются только
    /// в команде запуска: выбор «видеокарта» проверяется настоящим пробным кодированием.
    /// </summary>
    public string VideoEncoder { get; init; } = DefaultVideoEncoder;

    /// <summary>
    /// Пресет кодека: ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow.
    /// Список общий у всех кодеков, но у каждого своя шкала: у H.265 каждый шаг дороже, а у
    /// кодировщиков видеокарты своих имён в этом списке нет вовсе, и значения переводятся.
    /// </summary>
    public string CodecPreset { get; init; } = "medium";

    public int AudioBitrateKbps { get; init; } = 192;

    /// <summary>
    /// Кодировщик звука AAC: nmr, twoloop или fast. Список - в <see cref="AacCoders"/>.
    /// Имена оставлены как в ffmpeg: подписывать их своими словами означало бы обещать,
    /// что какие-то из них надёжнее остальных, а это проверено только на одном треке.
    /// </summary>
    public string AacCoder { get; init; } = DefaultAacCoder;

    /// <summary>Путь к ffmpeg.exe. Пусто - искать в PATH.</summary>
    public string? FfmpegPath { get; init; }
}