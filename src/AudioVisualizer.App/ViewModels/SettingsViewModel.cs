using System.Collections.ObjectModel;
using System.ComponentModel;
using AudioVisualizer.Core.Export;
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
    private readonly IVideoEncoderDetector _encoderDetector;

    private bool _suppressChanged;
    private bool _applyingPreset;

    public SettingsViewModel(
        AppSettings settings,
        ExportPresetStore? presetStore = null,
        IVideoEncoderDetector? encoderDetector = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _presetStore = presetStore ?? new ExportPresetStore();

        // В приложении зонд один на всё окно: панель и выгрузка спрашивают одно и то же, а
        // каждая проба - это запуск процесса, поэтому второй раз её запускать незачем.
        _encoderDetector = encoderDetector ?? new VideoEncoderDetector();

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
    /// Чем кодируется видео. Первые два пункта есть всегда, найденные вендоры добавляются
    /// зондом: показывать видеокарту, которой на машине нет, незачем, а вот молча убрать
    /// выбранную нельзя - она остаётся в списке с пометкой «не найдена».
    /// </summary>
    public ObservableCollection<Choice<string>> EncoderChoices { get; } =
    [
        new(ExportSettings.SoftwareVideoEncoder, "Программно (процессор)"),
        new(ExportSettings.AutoVideoEncoder, "Авто — видеокарта, если есть")
    ];

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

    /// <summary>Идёт ли проба видеокарты. Пока она идёт, в списке только два пункта.</summary>
    [ObservableProperty] private bool _isDetectingEncoders;

    /// <summary>
    /// Что нашлось или почему ничего: пустая строка, когда всё в порядке и видеокарта есть.
    /// </summary>
    [ObservableProperty] private string _encodersMessage = string.Empty;

    /// <summary>
    /// Есть ли чему занимать строку под списком. Сама строка без этого свойства оставалась бы
    /// на месте пустой и держала бы место: при найденной видеокарте сообщения нет вовсе, а
    /// блок с отступом всё равно занимал бы высоту.
    /// </summary>
    public bool HasEncodersMessage => !string.IsNullOrEmpty(EncodersMessage);

    private bool _refreshingEncoders;

    /// <summary>Идёт ли сборка списка кодировщиков: записи из привязки на это время игнорируются.</summary>
    private bool _fillingEncoders;

    /// <summary>Идёт ли возврат выбора после сборки списка: это не правка, и пресет она не сбрасывает.</summary>
    private bool _restoringEncoder;

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
    [ObservableProperty] private double _barTrailDensity;

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
    [ObservableProperty] private string _videoEncoder = ExportSettings.DefaultVideoEncoder;
    [ObservableProperty] private string _videoCodec = ExportSettings.DefaultVideoCodec;
    [ObservableProperty] private int _crf;
    [ObservableProperty] private string _codecPreset = "medium";
    [ObservableProperty] private int _audioBitrateKbps;
    [ObservableProperty] private string _ffmpegPath = string.Empty;

    /// <summary>
    /// Кодек видео. В списке показан как H.264 и H.265, в настройках и в команде ffmpeg
    /// остаётся имя кодировщика: подписьми своими словами мы бы обещали совместимость,
    /// которой у H.265 в старых проигрывателях нет.
    /// </summary>
    public IReadOnlyList<Choice<string>> VideoCodecChoices { get; } =
    [
        new(ExportSettings.VideoCodecs[0], "H.264"),
        new(ExportSettings.VideoCodecs[1], "H.265")
    ];

    /// <summary>
    /// Кодировщик звука AAC. Имена оставлены как в ffmpeg и без своих пометок: проверено
    /// лишь, что два других кодировщика не зависали на том треке, где зависает этот.
    /// Подписывать список значило бы обещать больше, чем показали проверки.
    /// </summary>
    [ObservableProperty] private string _aacCoder = ExportSettings.DefaultAacCoder;

    public IReadOnlyList<string> AacCoderChoices { get; } = ExportSettings.AacCoders;

    /// <summary>Тихая выгрузка: галочка отключает окно об успехе, но не об ошибке.</summary>
    [ObservableProperty] private bool _quietExport;

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
            TrailDecayMs = BarTrailDecayMs,
            TrailDensity = BarTrailDensity
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
            // Пустое значение в файл не пишется: нормализация молча превратила бы его в
            // «программно», и выбранный кодировщик потерялся бы до следующего перезапуска.
            VideoEncoder = string.IsNullOrWhiteSpace(VideoEncoder)
                ? ExportSettings.DefaultVideoEncoder
                : VideoEncoder,
            VideoCodec = VideoCodec,
            Crf = Crf,
            CodecPreset = CodecPreset,
            AudioBitrateKbps = AudioBitrateKbps,
            AacCoder = AacCoder,
            FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPath) ? null : FfmpegPath
        },
        QuietExport = QuietExport
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
            BarTrailDensity = settings.Bars.TrailDensity;

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
            VideoEncoder = settings.Export.VideoEncoder;
            VideoCodec = settings.Export.VideoCodec;
            Crf = settings.Export.Crf;
            CodecPreset = settings.Export.CodecPreset;
            AudioBitrateKbps = settings.Export.AudioBitrateKbps;
            AacCoder = settings.Export.AacCoder;
            FfmpegPath = settings.Export.FfmpegPath ?? string.Empty;
            QuietExport = settings.QuietExport;

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
        // Сборка списка кодировщиков тоже не должна: она доставляет то же значение обратно.
        if (!_restoringEncoder && e.PropertyName is nameof(ExportWidth)
            or nameof(ExportHeight)
            or nameof(ExportFramesPerSecond)
            or nameof(VideoEncoder)
            or nameof(VideoCodec)
            or nameof(Crf)
            or nameof(CodecPreset)
            or nameof(AudioBitrateKbps)
            or nameof(AacCoder))
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

    /// <summary>
    /// Ищет кодировщики видеокарты и наполняет ими список. Зовётся при открытии вкладки
    /// «Экспорт» и сам собой после смены кодека или пути к ffmpeg.
    /// </summary>
    public async Task RefreshEncodersAsync(CancellationToken cancellationToken = default)
    {
        // Вкладку открывают подряд, а проба стоит запусков процессов. Второй раз за то же
        // самое ждать незачем, и второй вызов просто выходит.
        if (_refreshingEncoders)
        {
            return;
        }

        _refreshingEncoders = true;
        IsDetectingEncoders = true;
        EncodersMessage = "Проверяем видеокарту…";

        try
        {
            var detection = await _encoderDetector
                .DetectAsync(FfmpegPath, VideoCodec, cancellationToken)
                .ConfigureAwait(true);

            FillEncoderChoices(detection);
        }
        catch (OperationCanceledException)
        {
            // Закрытие окна во время пробы - обычное дело, а не ошибка настроек.
            EncodersMessage = string.Empty;
        }
        catch (Exception)
        {
            // Зонд не должен ронять панель: список просто останется из двух пунктов, а о том,
            // что ffmpeg нет, выгрузка скажет сама и внятнее.
            EncodersMessage = "Проверить видеокарту не удалось.";
        }
        finally
        {
            IsDetectingEncoders = false;
            _refreshingEncoders = false;
        }
    }

    private void FillEncoderChoices(VideoEncoderDetection detection)
    {
        var found = detection.Supported;

        // Значение берётся до касания коллекции: очистка списка обнуляет его в привязке, и
        // опереться на свойство после этого нельзя - оно там уже другое.
        var current = string.IsNullOrWhiteSpace(VideoEncoder) ? ExportSettings.DefaultVideoEncoder : VideoEncoder;

        _fillingEncoders = true;

        try
        {
            EncoderChoices.Clear();
            EncoderChoices.Add(new Choice<string>(ExportSettings.SoftwareVideoEncoder, "Программно (процессор)"));
            EncoderChoices.Add(new Choice<string>(ExportSettings.AutoVideoEncoder, "Авто — видеокарта, если есть"));

            foreach (var support in found)
            {
                EncoderChoices.Add(new Choice<string>(support.Vendor, support.Title));
            }

            // Выбранного вендора в списке может не оказаться: карту унесли, или её не было, или
            // сменился путь к ffmpeg. Убрать выбор значило бы сбросить настройку молча, поэтому он
            // остаётся в списке с пометкой - иначе непонятно, куда делся выбор из файла настроек.
            var missing = VideoEncoderDetector.Vendors
                .FirstOrDefault(vendor =>
                    string.Equals(vendor.Vendor, current, StringComparison.Ordinal)
                    && found.All(support => !string.Equals(support.Vendor, vendor.Vendor, StringComparison.Ordinal)));

            if (missing is not null)
            {
                EncoderChoices.Add(new Choice<string>(missing.Vendor, $"{missing.Title} (не найдена)"));
            }
        }
        finally
        {
            _fillingEncoders = false;
        }

        // Значение возвращается и отправляется подписке ещё раз: после очистки списка цель
        // привязки осталась в null, и без уведомления поле было бы пустым при верном свойстве.
        // Отправка помечена как восстановление: это доставка того же значения в список, а не
        // правка человеком, и переопределение OnPropertyChanged по ней не сбрасывает пресет.
        _restoringEncoder = true;

        try
        {
            VideoEncoder = current;
            OnPropertyChanged(nameof(VideoEncoder));
        }
        finally
        {
            _restoringEncoder = false;
        }

        EncodersMessage = DescribeDetection(detection);
    }

    /// <summary>
    /// Что сказать под списком. Пустой ответ разбирается на две разные причины: нет ffmpeg —
    /// это вопрос к пути в настройках, а нет видеокарты — вопрос к железу. Раньше обе причины
    /// выглядели одинаково, и потерявшийся из PATH ffmpeg объявлялся отсутствием видеокарты.
    /// </summary>
    private static string DescribeDetection(VideoEncoderDetection detection)
    {
        if (detection.Supported.Count > 0)
        {
            return string.Empty;
        }

        return detection.FfmpegAvailable
            ? "Видеокарта с кодировщиком не найдена: кодировать будет процессор."
            : "ffmpeg не найден: проверьте путь к нему выше, кодировать на видеокарте нечем.";
    }

    /// <summary>Кодировщик привязан к коду и к ffmpeg, поэтому смена любого из них требует новой пробы.</summary>
    partial void OnVideoCodecChanged(string value) => RequestEncoderRefresh();

    partial void OnFfmpegPathChanged(string value) => RequestEncoderRefresh();

    partial void OnEncodersMessageChanged(string value) => OnPropertyChanged(nameof(HasEncodersMessage));

    /// <summary>
    /// Список пересобирается на ходу, и ComboBox на очистке приводит значение в null, а двусторонней
    /// привязкой это null попадает в настройку. Пока список собирается, запись игнорируется: выбор
    /// возвращается в <see cref="FillEncoderChoices"/> сразу после сборки.
    /// </summary>
    partial void OnVideoEncoderChanged(string value)
    {
        if (_fillingEncoders)
        {
            return;
        }

        RequestEncoderRefresh();
    }

    private void RequestEncoderRefresh()
    {
        // Программная правка настроек (Apply, выбор пресета) не должна дёргать зонд: список
        // и так наполняется при открытии вкладки, а лишний запуск процессов на каждый
        // пересозданный пресет - это десятки миллисекунд впустую.
        if (_suppressChanged)
        {
            return;
        }

        _ = RefreshEncodersAsync(CancellationToken.None);
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
            VideoEncoder = export.VideoEncoder;
            VideoCodec = export.VideoCodec;
            Crf = export.Crf;
            CodecPreset = export.CodecPreset;
            AudioBitrateKbps = export.AudioBitrateKbps;
            AacCoder = export.AacCoder;
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