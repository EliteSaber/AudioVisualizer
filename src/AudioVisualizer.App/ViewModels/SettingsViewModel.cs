using System.Collections.ObjectModel;
using System.ComponentModel;
using AudioVisualizer.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioVisualizer.App.ViewModels;

/// <summary>
/// Редактируемая копия настроек. Панель правит её напрямую, а модель окна по событию
/// <see cref="Changed"/> пересобирает кадр превью и предлагает сохранить файл.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ExportPresetStore _presetStore;

    private bool _suppressChanged;
    private bool _applyingPreset;

    public SettingsViewModel(AppSettings settings, ExportPresetStore? presetStore = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _presetStore = presetStore ?? new ExportPresetStore();

        // Встроенные идут первыми и не удаляются: свой пресет с тем же именем заменяет
        // встроенный на месте, а не добавляется вторым пунктом с тем же именем.
        ExportPresets = [.. BuiltInExportPresets.All];
        foreach (var preset in _presetStore.Load())
        {
            ExportPresets.Add(preset);
        }

        Apply(settings);
    }

    /// <summary>Изменилось любое поле панели.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<string> CodecPresets { get; } =
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"
    ];

    public IReadOnlyList<Choice<CornerStyle>> CornerStyleChoices { get; } =
    [
        new(CornerStyle.Rectangular, "Прямые"),
        new(CornerStyle.Rounded, "Скруглённые"),
        new(CornerStyle.OuterRounded, "Скруглённые снаружи")
    ];

    public IReadOnlyList<Choice<BarFillMode>> FillModeChoices { get; } =
    [
        new(BarFillMode.Solid, "Однотонная"),
        new(BarFillMode.VerticalGradient, "Градиент от центра")
    ];

    public IReadOnlyList<Choice<string>> SizePresets { get; } =
    [
        new("1080p60", "1080p, 60 кадров"),
        new("1080p30", "1080p, 30 кадров"),
        new("720p30", "720p, 30 кадров")
    ];

    public ObservableCollection<EqualizerBandViewModel> Equalizer { get; } = [];

    /// <summary>
    /// Пресеты выгрузки: сначала встроенные, потом сохранённые пользователем. Коллекция
    /// меняется на месте, а не пересоздаётся - иначе выбранный пункт списка теряется.
    /// </summary>
    public ObservableCollection<ExportPreset> ExportPresets { get; }

    /// <summary>
    /// Выбранный пресет выгрузки. Применяет все параметры ролика, а при ручной правке
    /// сбрасывается, чтобы список не показывал неверный набор.
    /// </summary>
    [ObservableProperty]
    private ExportPreset? _selectedExportPreset;

    /// <summary>Показывается ли поле ввода имени нового пресета.</summary>
    [ObservableProperty] private bool _isNamingExportPreset;

    [ObservableProperty] private string _newExportPresetName = string.Empty;

    /// <summary>Заметка о последней операции с пресетами: пустая, если всё прошло.</summary>
    [ObservableProperty] private string _presetsMessage = string.Empty;

    public bool CanDeleteExportPreset => SelectedExportPreset?.Custom == true;

    public bool CanSaveExportPreset => !string.IsNullOrWhiteSpace(NewExportPresetName);

    [ObservableProperty] private RgbaColor _backgroundColor;
    [ObservableProperty] private RgbaColor _barFillColor;
    [ObservableProperty] private RgbaColor _barSecondaryColor;
    [ObservableProperty] private RgbaColor _barBorderColor;
    [ObservableProperty] private RgbaColor _centerLineColor;

    [ObservableProperty] private int _barWidth;
    [ObservableProperty] private int _barSpacing;
    [ObservableProperty] private int _barCornerRadius;
    [ObservableProperty] private CornerStyle _barCornerStyle;
    [ObservableProperty] private double _barBorderThickness;
    [ObservableProperty] private BarFillMode _barFillMode;
    [ObservableProperty] private int _barMargin;
[ObservableProperty] private int _barCenterGap;
    [ObservableProperty] private bool _barMirror;
    [ObservableProperty] private double _barTrailDecayMs;

    [ObservableProperty] private int _fftSize;
    [ObservableProperty] private double _minFrequencyHz;
    [ObservableProperty] private double _maxFrequencyHz;
    [ObservableProperty] private double _minDecibels;
    [ObservableProperty] private double _maxDecibels;
    [ObservableProperty] private double _gainDb;
    [ObservableProperty] private double _gamma;
    [ObservableProperty] private double _attack;
    [ObservableProperty] private double _release;

    [ObservableProperty] private int _exportWidth;
    [ObservableProperty] private int _exportHeight;
    [ObservableProperty] private int _exportFramesPerSecond;
    [ObservableProperty] private int _crf;
    [ObservableProperty] private string _codecPreset = "medium";
    [ObservableProperty] private int _audioBitrateKbps;
    [ObservableProperty] private string _ffmpegPath = string.Empty;

    public AppSettings ToAppSettings() => new()
    {
        Colors = new ColorSettings
        {
            Background = BackgroundColor,
            BarFill = BarFillColor,
            BarFillSecondary = BarSecondaryColor,
            BarBorder = BarBorderColor,
            CenterLine = CenterLineColor
        },
        Bars = new BarSettings
        {
            Width = BarWidth,
            Spacing = BarSpacing,
            CornerRadius = BarCornerRadius,
            CornerStyle = BarCornerStyle,
            BorderWidth = BarBorderThickness,
            FillMode = BarFillMode,
            Margin = BarMargin,
CenterGap = BarCenterGap,
            Mirror = BarMirror,
            TrailDecayMs = BarTrailDecayMs
        },
        Spectrum = new SpectrumSettings
        {
            FftSize = FftSize,
            MinFrequencyHz = (float)MinFrequencyHz,
            MaxFrequencyHz = (float)MaxFrequencyHz,
            MinDecibels = (float)MinDecibels,
            MaxDecibels = (float)MaxDecibels,
            GainDb = (float)GainDb,
            Gamma = (float)Gamma,
            Attack = (float)Attack,
            Release = (float)Release,
            Equalizer = Equalizer.Select(band => new EqualizerBand(band.CenterFrequencyHz, band.GainDb)).ToArray()
        },
        Export = new ExportSettings
        {
            Width = ExportWidth,
            Height = ExportHeight,
            FramesPerSecond = ExportFramesPerSecond,
            Crf = Crf,
            CodecPreset = CodecPreset,
            AudioBitrateKbps = AudioBitrateKbps,
            FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPath) ? null : FfmpegPath
        }
    };

    /// <summary>Загружает значения в панель и поднимает <see cref="Changed"/> один раз, а не на каждое поле.</summary>
    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var spectrum = settings.Spectrum;

        _suppressChanged = true;
        try
        {
            BackgroundColor = settings.Colors.Background;
            BarFillColor = settings.Colors.BarFill;
            BarSecondaryColor = settings.Colors.BarFillSecondary;
            BarBorderColor = settings.Colors.BarBorder;
            CenterLineColor = settings.Colors.CenterLine;

            BarWidth = settings.Bars.Width;
            BarSpacing = settings.Bars.Spacing;
            BarCornerRadius = settings.Bars.CornerRadius;
            BarCornerStyle = settings.Bars.CornerStyle;
            BarBorderThickness = settings.Bars.BorderWidth;
            BarFillMode = settings.Bars.FillMode;
            BarMargin = settings.Bars.Margin;
BarCenterGap = settings.Bars.CenterGap;
            BarMirror = settings.Bars.Mirror;
            BarTrailDecayMs = settings.Bars.TrailDecayMs;

            FftSize = spectrum.FftSize;
            MinFrequencyHz = spectrum.MinFrequencyHz;
            MaxFrequencyHz = spectrum.MaxFrequencyHz;
            MinDecibels = spectrum.MinDecibels;
            MaxDecibels = spectrum.MaxDecibels;
            GainDb = spectrum.GainDb;
            Gamma = spectrum.Gamma;
            Attack = spectrum.Attack;
            Release = spectrum.Release;

            ExportWidth = settings.Export.Width;
            ExportHeight = settings.Export.Height;
            ExportFramesPerSecond = settings.Export.FramesPerSecond;
            Crf = settings.Export.Crf;
            CodecPreset = settings.Export.CodecPreset;
            AudioBitrateKbps = settings.Export.AudioBitrateKbps;
            FfmpegPath = settings.Export.FfmpegPath ?? string.Empty;

            ApplyEqualizer(spectrum.Equalizer);
        }
        finally
        {
            _suppressChanged = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Правка любого параметра ролика означает, что пресет больше не описывает текущие
        // настройки. Путь к ffmpeg в список не входит, поэтому его правка выбор не сбрасывает.
        if (e.PropertyName is nameof(ExportWidth)
            or nameof(ExportHeight)
            or nameof(ExportFramesPerSecond)
            or nameof(Crf)
            or nameof(CodecPreset)
            or nameof(AudioBitrateKbps))
        {
            ResetExportPreset();
        }

        if (e.PropertyName is nameof(SelectedExportPreset))
        {
            OnPropertyChanged(nameof(CanDeleteExportPreset));
            DeleteExportPresetCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName is nameof(NewExportPresetName))
        {
            SaveExportPresetCommand.NotifyCanExecuteChanged();
        }

        if (!_suppressChanged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Показывает поле ввода имени с текущим размером в качестве подсказки.</summary>
    [RelayCommand]
    private void StartNamingExportPreset()
    {
        NewExportPresetName = $"{ExportWidth}x{ExportHeight}, {ExportFramesPerSecond}";
        PresetsMessage = string.Empty;
        IsNamingExportPreset = true;
    }

    [RelayCommand]
    private void CancelNamingExportPreset()
    {
        IsNamingExportPreset = false;
        NewExportPresetName = string.Empty;
    }

    /// <summary>Запоминает текущие параметры под введённым именем.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveExportPreset))]
    private void SaveExportPreset()
    {
        var name = NewExportPresetName.Trim();
        var preset = new ExportPreset
        {
            Name = name,
            Settings = CurrentExport() with { FfmpegPath = null },
            Custom = true
        };

// Повторное имя обновляет существующий пресет на месте: два пункта с одним именем
        // в списке выглядели бы как ошибка.
        var index = IndexOfPreset(name);

        if (index >= 0 && !ExportPresets[index].Custom)
        {
            // Имя встроенного пресета занято: подменив его своим, пользователь потерял бы
            // встроенный до перезапуска программы.
            PresetsMessage = $"Имя «{name}» занято встроенным пресетом. Придумайте другое.";
            return;
        }

        if (index < 0)
        {
            ExportPresets.Add(preset);
        }
        else
        {
            ExportPresets[index] = preset;
        }

        SavePresetsToFile();

        SelectedExportPreset = preset;
        IsNamingExportPreset = false;
        NewExportPresetName = string.Empty;
    }

    /// <summary>Удаляет пользовательский пресет. Встроенные удалить нельзя.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteExportPreset))]
    private void DeleteExportPreset()
    {
        var preset = SelectedExportPreset;

        if (preset is null || !preset.Custom)
        {
            return;
        }

        ExportPresets.Remove(preset);
        SelectedExportPreset = null;
        PresetsMessage = string.Empty;

        SavePresetsToFile();
    }

    [RelayCommand]
    private void ResetColors()
    {
        var current = ToAppSettings();
        Apply(current with { Colors = new ColorSettings() });
    }

    [RelayCommand]
    private void ResetBars()
    {
        var current = ToAppSettings();
        Apply(current with { Bars = new BarSettings() });
    }

    [RelayCommand]
    private void ResetSpectrum()
    {
        var current = ToAppSettings();
        Apply(current with { Spectrum = new SpectrumSettings() });
    }

    /// <summary>
    /// Возвращает параметры ролика к умолчаниям, сохраняя путь к ffmpeg: он задаётся один
    /// раз на машину и не должен пропадать при сбросе настроек видео.
    /// </summary>
    [RelayCommand]
    private void ResetExport()
    {
        var current = ToAppSettings();
        Apply(current with { Export = new ExportSettings { FfmpegPath = current.Export.FfmpegPath } });
    }

    /// <summary>Применяет пресет: все параметры ролика, кроме пути к ffmpeg.</summary>
    public void ApplyExportPreset(ExportPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var export = SettingsNormalizer.NormalizeExport(preset.Settings);

        _applyingPreset = true;
        try
        {
            ExportWidth = export.Width;
            ExportHeight = export.Height;
            ExportFramesPerSecond = export.FramesPerSecond;
            Crf = export.Crf;
            CodecPreset = export.CodecPreset;
            AudioBitrateKbps = export.AudioBitrateKbps;
        }
        finally
        {
            _applyingPreset = false;
        }
    }

    private void ResetExportPreset()
    {
        if (!_applyingPreset && SelectedExportPreset is not null)
        {
            SelectedExportPreset = null;
        }
    }

    private void SavePresetsToFile()
    {
        var saved = _presetStore.Save(ExportPresets.Where(preset => preset.Custom).ToArray());

        PresetsMessage = saved
            ? string.Empty
            : "Не удалось сохранить пресеты: файл " + _presetStore.FilePath + " недоступен.";
    }

    private int IndexOfPreset(string name)
    {
        for (var index = 0; index < ExportPresets.Count; index++)
        {
            if (string.Equals(ExportPresets[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private ExportSettings CurrentExport() => ToAppSettings().Export;

    partial void OnSelectedExportPresetChanged(ExportPreset? value)
    {
        if (value is not null)
        {
            ApplyExportPreset(value);
        }
    }

    private void ApplyEqualizer(IReadOnlyList<EqualizerBand> bands)
    {
        Equalizer.Clear();

        foreach (var band in bands)
        {
            Equalizer.Add(new EqualizerBandViewModel(band.CenterFrequencyHz, band.GainDb, () => Changed?.Invoke(this, EventArgs.Empty)));
        }
    }
}