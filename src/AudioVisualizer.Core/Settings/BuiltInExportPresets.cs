namespace AudioVisualizer.Core.Settings;

/// <summary>
/// Пресеты выгрузки, которые идут в комплекте и удалить их нельзя.
/// </summary>
/// <remarks>
/// Каждый задаёт весь набор параметров ролика, а не только размер: выбранный пресет должен
/// сразу давать готовый результат. Общие значения совпадают с умолчаниями
/// <see cref="ExportSettings"/>.
/// </remarks>
public static class BuiltInExportPresets
{
    public static IReadOnlyList<ExportPreset> All { get; } =
    [
        new()
        {
            Name = "1080p60",
            Settings = new ExportSettings
            {
                Width = 1920,
                Height = 1080,
                FramesPerSecond = 60,
                Crf = 18,
                CodecPreset = "medium",
                AudioBitrateKbps = 192
            }
        },
        new()
        {
            Name = "1080p30",
            Settings = new ExportSettings
            {
                Width = 1920,
                Height = 1080,
                FramesPerSecond = 30,
                Crf = 18,
                CodecPreset = "medium",
                AudioBitrateKbps = 192
            }
        },
        new()
        {
            Name = "720p30",
            Settings = new ExportSettings
            {
                Width = 1280,
                Height = 720,
                FramesPerSecond = 30,
                Crf = 18,
                CodecPreset = "medium",
                AudioBitrateKbps = 192
            }
        }
    ];
}