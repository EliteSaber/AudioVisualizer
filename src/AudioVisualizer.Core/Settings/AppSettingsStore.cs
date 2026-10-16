using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioVisualizer.Core.Settings;

/// <summary>
/// Чтение и запись файла настроек рядом с программой. Как и в остальных настройках, файл
/// принадлежит копии программы и правится руками, поэтому чтение никогда не бросает:
/// негодный файл даёт значения по умолчанию и остаётся на месте для разбора.
/// </summary>
public sealed class AppSettingsStore
{
    public const string FileName = "settings.json";

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

    public AppSettingsStore()
        : this(Resolve())
    {
    }

    /// <param name="path">Путь к файлу. Тестам нужен свой путь, иначе они испортили бы настройки запущенной копии.</param>
    public AppSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    public string FilePath => _path;

    /// <summary>Каталог программы, а не рабочий: рабочий у процесса свой и зависит от способа запуска.</summary>
    public static string Resolve() => Path.Combine(AppContext.BaseDirectory, FileName);

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            var defaults = SettingsNormalizer.Normalize(new AppSettings());
            Save(defaults);

            return defaults;
        }

        var loaded = TryRead();

        return loaded is null ? SettingsNormalizer.Normalize(new AppSettings()) : SettingsNormalizer.Normalize(loaded);
    }

    /// <summary>
    /// Пишет настройки. При неудаче отдаёт причину: раньше она молча терялась, и о том, что
    /// файл не записан, пользователь узнавал только при следующем запуске.
    /// </summary>
    public bool Save(AppSettings settings, out string? failure)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(SettingsNormalizer.Normalize(settings), WriteOptions));

            failure = null;

            return true;
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            failure = exception.Message;

            return false;
        }
    }

    public bool Save(AppSettings settings) => Save(settings, out _);

    private AppSettings? TryRead()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), ReadOptions);
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return null;
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