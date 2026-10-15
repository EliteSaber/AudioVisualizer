namespace AudioVisualizer.Core.Settings;

/// <summary>Настройки приложения целиком: то, что лежит в settings.json рядом с программой.</summary>
public sealed record AppSettings
{
    public ColorSettings Colors { get; init; } = new();

    public BarSettings Bars { get; init; } = new();

    public SpectrumSettings Spectrum { get; init; } = new();

    public ExportSettings Export { get; init; } = new();

    /// <summary>
    /// Не показывать окно об удачной выгрузке.
    /// </summary>
    /// <remarks>
    /// Живёт здесь, а не в <see cref="ExportSettings"/>, потому что тот уезжает в пресеты ролика,
    /// а это свойство машины, а не картинки. Ошибки галочка не отключает: их и не бывает
    /// слишком много, а потерять их молча нельзя.
    /// </remarks>
    public bool QuietExport { get; init; }

    public static AppSettings Default { get; } = new();
}