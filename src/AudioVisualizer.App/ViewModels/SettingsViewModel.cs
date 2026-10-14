using System.Collections.ObjectModel;
using System.ComponentModel;
using AudioVisualizer.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioVisualizer.App.ViewModels;

/// <summary>
/// Редактируемая копия настроек. Панель правит её напрямую, а модель окна по событию
/// <see cref="Changed"/> пересобирает кадр превью и предлагает сохранить файл.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private bool _suppressChanged;
    private bool _applyingSizePreset;
    private Choice<string>? _selectedSizePreset;

    public SettingsViewModel(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

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
    /// Выбранный пресет размера кадра. Меняет ширину, высоту и частоту кадров, а при ручной
    /// правке ползунков сбрасывается, чтобы список не показывал неверный размер.
    /// </summary>
    public Choice<string>? SelectedSizePreset
    {
        get => _selectedSizePreset;
        set
        {
            if (SetProperty(ref _selectedSizePreset, value) && value is not null)
            {
                ApplySizePreset(value.Value);
            }
        }
    }

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
            Mirror = BarMirror
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

        if (e.PropertyName is nameof(ExportWidth) or nameof(ExportHeight) or nameof(ExportFramesPerSecond))
        {
            ResetSizePreset();
        }

        if (!_suppressChanged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplySizePreset(string preset)
    {
        var (width, height, framesPerSecond) = preset switch
        {
            "1080p60" => (1920, 1080, 60),
            "1080p30" => (1920, 1080, 30),
            "720p30" => (1280, 720, 30),
            _ => (ExportWidth, ExportHeight, ExportFramesPerSecond)
        };

        _applyingSizePreset = true;
        try
        {
            ExportWidth = width;
            ExportHeight = height;
            ExportFramesPerSecond = framesPerSecond;
        }
        finally
        {
            _applyingSizePreset = false;
        }
    }

    private void ResetSizePreset()
    {
        if (!_applyingSizePreset && SelectedSizePreset is not null)
        {
            SelectedSizePreset = null;
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