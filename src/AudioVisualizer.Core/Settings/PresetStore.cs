using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioVisualizer.Core.Settings;

/// <summary>
/// Пресеты оформления: наборы настроек под именем. Лежат рядом с программой отдельными файлами,
/// чтобы пользователь мог заглянуть в них и подправить руками.
/// </summary>
public sealed class PresetStore
{
    public const string DirectoryName = "presets";
    public const string Extension = ".json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _directory;

    public PresetStore()
        : this(Resolve())
    {
    }

    public PresetStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = directory;
    }

    public static string Resolve() => Path.Combine(AppContext.BaseDirectory, DirectoryName);

    /// <summary>Имена пресетов по алфавиту, без расширения.</summary>
    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(_directory, $"*{Extension}")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public void Save(string name, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.Serialize(SettingsNormalizer.Normalize(settings), Options);
        File.WriteAllText(FilePathOf(name), json);
    }

    public AppSettings Load(string name)
    {
        var path = FilePathOf(name);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Пресет \"{name}\" не найден.", path);
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);

        return SettingsNormalizer.Normalize(settings ?? new AppSettings());
    }

    public bool Delete(string name)
    {
        var path = FilePathOf(name);

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);

        return true;
    }

    private string FilePathOf(string name)
        => Path.Combine(_directory, Sanitize(name) + Extension);

    /// <summary>Имя пресета превращается в безопасное имя файла: слеши и звёздочки в путь не пускаем.</summary>
    public static string Sanitize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();

        if (cleaned.Length == 0)
        {
            throw new ArgumentException("Имя пресета не может быть пустым.", nameof(name));
        }

        return cleaned;
    }
}