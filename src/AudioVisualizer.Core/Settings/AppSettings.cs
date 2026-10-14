namespace AudioVisualizer.Core.Settings;

/// <summary>Настройки приложения целиком: то, что лежит в settings.json рядом с программой.</summary>
public sealed record AppSettings
{
    public ColorSettings Colors { get; init; } = new();

    public BarSettings Bars { get; init; } = new();

    public SpectrumSettings Spectrum { get; init; } = new();

    public ExportSettings Export { get; init; } = new();

    public static AppSettings Default { get; } = new();
}