namespace AudioVisualizer.Core.Audio;

/// <summary>Поиск ffmpeg.exe: сначала путь из настроек, затем переменная PATH.</summary>
public sealed class FfmpegLocator
{
    public const string ExecutableName = "ffmpeg.exe";

    private readonly string? _configuredPath;
    private readonly string? _pathVariable;

    public FfmpegLocator(string? configuredPath = null)
        : this(configuredPath, Environment.GetEnvironmentVariable("PATH"))
    {
    }

    public FfmpegLocator(string? configuredPath, string? pathVariable)
    {
        _configuredPath = configuredPath;
        _pathVariable = pathVariable;
    }

    public string? Locate() => Locate(_configuredPath, _pathVariable, File.Exists);

    public string EnsureFound() => Locate() ?? throw new FfmpegNotFoundException();

    public static string? Locate(string? configuredPath, string? pathVariable, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = configuredPath.Trim();
            if (fileExists(configured))
            {
                return configured;
            }
        }

        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return null;
        }

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (directory.Length == 0)
            {
                continue;
            }

            string candidate;
            try
            {
                candidate = Path.Combine(directory, ExecutableName);
            }
            catch (ArgumentException)
            {
                // Некорректная запись в PATH - пропускаем, а не роняем поиск.
                continue;
            }

            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}