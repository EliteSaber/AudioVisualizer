using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioVisualizer.Core.Settings;

/// <summary>
/// Список пользовательских пресетов выгрузки. Лежит отдельным файлом рядом с настройками,
/// чтобы пресет сохранялся сразу, а не по кнопке «сохранить настройки».
/// </summary>
/// <remarks>
/// Как и файл настроек, он принадлежит копии программы и правится руками, поэтому чтение
/// никогда не бросает: негодный файл даёт пустой список и остаётся на месте для разбора.
/// </remarks>
public sealed class ExportPresetStore
{
    public const string FileName = "exportpresets.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public ExportPresetStore()
        : this(Resolve())
    {
    }

    /// <param name="path">Путь к файлу. Тестам нужен свой путь, иначе они портили бы пресеты запущенной копии.</param>
    public ExportPresetStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    public string FilePath => _path;

    /// <summary>Каталог программы, а не рабочий: рабочий у процесса свой и зависит от способа запуска.</summary>
    public static string Resolve() => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>Пресеты из файла. Всё, что нашлось, - пользовательское.</summary>
    public IReadOnlyList<ExportPreset> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var stored = JsonSerializer.Deserialize<List<ExportPreset>>(File.ReadAllText(_path), ReadOptions);

            return SettingsNormalizer
                .NormalizeExportPresets(stored ?? [])
                .Select(preset => preset with { Custom = true })
                .ToArray();
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return [];
        }
    }

    /// <summary>Пишет список пресетов. Путь к ffmpeg вычищается: это свойство машины.</summary>
    public bool Save(IReadOnlyList<ExportPreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        try
        {
            var prepared = SettingsNormalizer
                .NormalizeExportPresets(presets)
                .Select(preset => preset with
                {
                    Settings = preset.Settings with { FfmpegPath = null },
                    Custom = false
                })
                .ToArray();

            File.WriteAllText(_path, JsonSerializer.Serialize(prepared, WriteOptions));

            return true;
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return false;
        }
    }

    // Ловить Exception подряд нельзя: под «всё» попал бы OutOfMemoryException.
    private static bool IsUnreadable(Exception exception) => exception is JsonException or NotSupportedException || IsFileProblem(exception);

    private static bool IsFileProblem(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException;
}