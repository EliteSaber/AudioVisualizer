using System.Text.Json.Serialization;

namespace AudioVisualizer.Core.Settings;

/// <summary>
/// Пресет выгрузки: имя и набор параметров видео.
/// </summary>
/// <remarks>
/// Пресет - это весь <see cref="ExportSettings"/>, кроме пути к ffmpeg: путь принадлежит
/// машине, а не ролику, и переносить его между компьютерами незачем. Встроенные пресеты
/// помечены <see cref="Custom"/> = false и удалить их нельзя.
/// </remarks>
public sealed record ExportPreset
{
    public string Name { get; init; } = string.Empty;

    public ExportSettings Settings { get; init; } = new();

    /// <summary>Пресет из файла пользователя. В файле не хранится: там всё и так своё.</summary>
    [JsonIgnore]
    public bool Custom { get; init; }
}