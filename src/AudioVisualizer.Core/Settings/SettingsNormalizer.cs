namespace AudioVisualizer.Core.Settings;

/// <summary>
/// Приводит настройки к пригодным значениям: файл настроек правят руками, и полусантиметровая
/// ширина столбика или нечётная ширина кадра должны исправляться, а не ронять экспорт.
/// </summary>
public static class SettingsNormalizer
{
    public const int MinimumFftSize = 256;
    public const int MaximumFftSize = 16384;
    public const float MinimumFrequencyHz = 20f;
    public const float MaximumFrequencyHz = 22050f;
    public const float EqualizerGainLimitDb = 12f;

    private static readonly string[] CodecPresets =
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"
    ];

    public static AppSettings Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings with
        {
            // Разбор JSON не мешает явному null в полях: подставляем значения по умолчанию.
            Colors = settings.Colors ?? new ColorSettings(),
            Bars = NormalizeBars(settings.Bars ?? new BarSettings()),
            Spectrum = NormalizeSpectrum(settings.Spectrum ?? new SpectrumSettings()),
            Export = NormalizeExport(settings.Export ?? new ExportSettings())
        };
    }

    public static BarSettings NormalizeBars(BarSettings bars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        var width = Math.Clamp(bars.Width, 1, 200);

        return bars with
        {
            Width = width,
            Spacing = Math.Clamp(bars.Spacing, 0, 200),
            // Радиус больше половины ширины WPF всё равно схлопывает в полукруг,
            // а расчёт геометрии в рендерере ожидает честное число.
            CornerRadius = Math.Clamp(bars.CornerRadius, 0, width / 2),
            BorderWidth = Math.Clamp(bars.BorderWidth, 0d, 20d),
            Margin = Math.Clamp(bars.Margin, 0, 2000),
            CenterGap = Math.Clamp(bars.CenterGap, 0, 500),
            TrailDecayMs = Math.Clamp(bars.TrailDecayMs, 0d, 5000d),
            TrailDensity = Math.Clamp(bars.TrailDensity, 0d, 1d),
            CornerStyle = Enum.IsDefined(bars.CornerStyle) ? bars.CornerStyle : CornerStyle.OuterRounded,
            FillMode = Enum.IsDefined(bars.FillMode) ? bars.FillMode : BarFillMode.VerticalGradient
        };
    }

    public static SpectrumSettings NormalizeSpectrum(SpectrumSettings spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        var maxFrequency = Math.Clamp(spectrum.MaxFrequencyHz, MinimumFrequencyHz + 1f, MaximumFrequencyHz);
        var minFrequency = Math.Clamp(spectrum.MinFrequencyHz, MinimumFrequencyHz, MaximumFrequencyHz);

        // Порядок задаётся явно: иначе «частота выше 22 кГц» дала бы нижнюю границу
        // больше верхней и Math.Clamp ругнулся бы на неверные границы.
        if (maxFrequency <= minFrequency)
        {
            maxFrequency = Math.Min(MaximumFrequencyHz, minFrequency + 1f);
        }

        var minDecibels = Math.Min(spectrum.MinDecibels, spectrum.MaxDecibels - 1f);

        return spectrum with
        {
            FftSize = NearestPowerOfTwo(Math.Clamp(spectrum.FftSize, MinimumFftSize, MaximumFftSize)),
            AnalysisBandCount = Math.Clamp(spectrum.AnalysisBandCount, 8, 1024),
            MinFrequencyHz = minFrequency,
            MaxFrequencyHz = maxFrequency,
            MinDecibels = minDecibels,
            MaxDecibels = spectrum.MaxDecibels,
            Gamma = Math.Clamp(spectrum.Gamma, 0.1f, 4f),
            Attack = Math.Clamp(spectrum.Attack, 0f, 1f),
            Release = Math.Clamp(spectrum.Release, 0f, 1f),
            Equalizer = NormalizeEqualizer(spectrum.Equalizer)
        };
    }

    public static ExportSettings NormalizeExport(ExportSettings export)
    {
        ArgumentNullException.ThrowIfNull(export);

        var preset = export.CodecPreset?.Trim().ToLowerInvariant();
        var coder = export.AacCoder?.Trim().ToLowerInvariant();

        return export with
        {
            // yuv420p требует чётных размеров: нечётный кадр ffmpeg завернёт ошибкой.
            Width = ToEven(Math.Clamp(export.Width, 64, 7680)),
            Height = ToEven(Math.Clamp(export.Height, 64, 7680)),
            FramesPerSecond = Math.Clamp(export.FramesPerSecond, 1, 240),
            Crf = Math.Clamp(export.Crf, 0, 51),
            CodecPreset = preset is not null && CodecPresets.Contains(preset, StringComparer.OrdinalIgnoreCase) ? preset : "medium",
            AudioBitrateKbps = Math.Clamp(export.AudioBitrateKbps, 32, 320),
            AacCoder = coder is not null && ExportSettings.AacCoders.Contains(coder, StringComparer.OrdinalIgnoreCase)
                ? coder
                : ExportSettings.DefaultAacCoder,
            FfmpegPath = string.IsNullOrWhiteSpace(export.FfmpegPath) ? null : export.FfmpegPath.Trim()
        };
    }

    /// <summary>
    /// Приводит список пресетов выгрузки к пригодному виду: имя обрезается, значения
    /// нормализуются как обычные настройки экспорта, безымянные пресеты и повторы имён
    /// отбрасываются. Файл правится руками, поэтому дубль не должен превращаться в два
    /// одинаковых пункта списка.
    /// </summary>
    public static IReadOnlyList<ExportPreset> NormalizeExportPresets(IEnumerable<ExportPreset>? presets)
    {
        if (presets is null)
        {
            return [];
        }

        var result = new List<ExportPreset>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var preset in presets)
        {
            if (preset is null)
            {
                continue;
            }

            var name = preset.Name?.Trim() ?? string.Empty;

            if (name.Length == 0 || !names.Add(name))
            {
                continue;
            }

            result.Add(preset with { Name = name, Settings = NormalizeExport(preset.Settings ?? new ExportSettings()) });
        }

        return result;
    }

    public static IReadOnlyList<EqualizerBand> NormalizeEqualizer(IReadOnlyList<EqualizerBand>? bands)
    {
        if (bands is null || bands.Count == 0)
        {
            return SpectrumSettings.DefaultEqualizer;
        }

        return bands
            .Select(band => band with
            {
                CenterFrequencyHz = Math.Clamp(band.CenterFrequencyHz, MinimumFrequencyHz, MaximumFrequencyHz),
                GainDb = Math.Clamp(band.GainDb, -EqualizerGainLimitDb, EqualizerGainLimitDb)
            })
            .OrderBy(band => band.CenterFrequencyHz)
            .ToArray();
    }

    private static int NearestPowerOfTwo(int value)
    {
        var power = MinimumFftSize;
        while (power < value && power < MaximumFftSize)
        {
            power <<= 1;
        }

        return power;
    }

    private static int ToEven(int value) => value % 2 == 0 ? value : value - 1;
}