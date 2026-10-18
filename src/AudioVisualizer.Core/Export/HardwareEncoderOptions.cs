using System.Globalization;

namespace AudioVisualizer.Core.Export;

/// <summary>
/// Параметры качества кодировщиков видеокарты. Нужны потому, что у них нет ни <c>-crf</c>,
/// ни наших пресетов: имена у всех свои, и пользователь выбирает из списка x264.
/// </summary>
/// <remarks>
/// Таблицы соответствий переводят наш список пресетов на имена каждого кодировщика, а наш
/// CRF - на его параметр качества. Шкала 0…51 общая и направлена одинаково: меньше - лучше,
/// поэтому ползунок в панели остаётся тем же самым. Проверено настоящей выгрузкой для
/// NVENC; QSV и AMF сверены по <c>ffmpeg -h encoder=…</c>, карт таких на машине разработки
/// не было.
/// </remarks>
public static class HardwareEncoderOptions
{
    /// <summary>Суффиксы имён кодировщиков ffmpeg. Порядок совпадает с порядком поиска.</summary>
    public static IReadOnlyList<string> Suffixes { get; } = ["nvenc", "qsv", "amf"];

    private static readonly Dictionary<string, string> NvencPresets = new(StringComparer.Ordinal)
    {
        ["ultrafast"] = "p1",
        ["superfast"] = "p1",
        ["veryfast"] = "p2",
        ["faster"] = "p3",
        ["fast"] = "p4",
        ["medium"] = "p4",
        ["slow"] = "p5",
        ["slower"] = "p6",
        ["veryslow"] = "p7"
    };

    // У QSV нет двух самых быстрых пресетов из нашего списка, оба сходятся в veryfast.
    private static readonly Dictionary<string, string> QsvPresets = new(StringComparer.Ordinal)
    {
        ["ultrafast"] = "veryfast",
        ["superfast"] = "veryfast",
        ["veryfast"] = "veryfast",
        ["faster"] = "faster",
        ["fast"] = "fast",
        ["medium"] = "medium",
        ["slow"] = "slow",
        ["slower"] = "slower",
        ["veryslow"] = "veryslow"
    };

    private static readonly Dictionary<string, string> AmfPresets = new(StringComparer.Ordinal)
    {
        ["ultrafast"] = "speed",
        ["superfast"] = "speed",
        ["veryfast"] = "speed",
        ["faster"] = "speed",
        ["fast"] = "balanced",
        ["medium"] = "balanced",
        ["slow"] = "quality",
        ["slower"] = "quality",
        ["veryslow"] = "quality"
    };

    /// <summary>
    /// Параметры качества для кодировщика видеокарты.
    /// </summary>
    /// <param name="suffix">Суффикс имени кодировщика: nvenc, qsv или amf.</param>
    /// <param name="preset">Наш пресет из списка настроек.</param>
    /// <param name="crf">Наше качество 0…51.</param>
    public static IReadOnlyList<string> Quality(string suffix, string preset, int crf)
    {
        var quality = Math.Clamp(crf, 0, 51).ToString(CultureInfo.InvariantCulture);

        return suffix switch
        {
            // -b:v 0 обязателен вместе с -cq: без него кодировщик режет поток по битрейту,
            // а не по качеству, и ползунок перестаёт что-либо значить.
            "nvenc" => ["-preset", NvencPresets[preset], "-rc", "vbr", "-cq", quality, "-b:v", "0"],
            "qsv" => ["-preset", QsvPresets[preset], "-global_quality", quality],
            "amf" => ["-quality", AmfPresets[preset], "-rc", "cqp", "-qp_i", quality, "-qp_p", quality],
            _ => throw new ArgumentException($"Неизвестный кодировщик видеокарты \"{suffix}\".", nameof(suffix))
        };
    }

    /// <summary>Суффикс кодировщика по его имени в ffmpeg или null, если имя не наше.</summary>
    public static string? SuffixOf(string? encoder)
    {
        var name = encoder?.Trim().ToLowerInvariant() ?? string.Empty;

        return Suffixes.FirstOrDefault(
            suffix => name.EndsWith('_' + suffix, StringComparison.Ordinal));
    }
}
