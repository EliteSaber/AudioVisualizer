using System.IO;
using System.Windows.Threading;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AudioVisualizer.App.ViewModels;

/// <summary>
/// Состояние окна: открытый файл, воспроизведение, кадр превью и панель настроек.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Фильтр диалога открытия в формате Win32: пары «описание | шаблон», расширения
    /// через точку с запятой. Строка вынесена наружу, потому что иначе её ошибку замечают
    /// только кликом по кнопке - диалог в тестах не открывается.
    /// </summary>
    public const string AudioFileFilter =
        "Аудио (*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.m4b)|" +
        "*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.m4b|" +
        "Все файлы (*.*)|*.*";

    /// <summary>Фильтр сохранения видео в формате Win32: та же ошибка видна только по клику.</summary>
    public const string VideoFileFilter = "Видео MP4 (*.mp4)|*.mp4";

    private const int MaximumFramesPerSecond = 60;

    /// <summary>Пауза после последней правки, через которую пишется файл настроек.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(10);

    /// <summary>Меньше этой разницы перемотка считается щелчком по бегунку без переноса.</summary>
    private const double MinimumSeekDeltaSeconds = 0.05d;

    private readonly AppSettingsStore _store;
    private readonly AudioLoader _loader;
    private readonly IAudioPlayer _player;
    private readonly IVideoExporter _exporter;

    private AudioData? _audio;
    private SpectrumFrameSource? _frames;
    private string? _audioPath;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _saveTimer;
    private CancellationTokenSource? _exportCancellation;
    private VideoExportProgress? _progress;
    private bool _isCancelling;
    private bool _isScrubbing;
    private bool _saveFailed;

    /// <summary>Ждёт своей очереди запись: правки есть, а файл с ними ещё не записан.</summary>
    internal bool SavePending { get; private set; }

    public MainViewModel(
        AppSettingsStore? store = null,
        AudioLoader? loader = null,
        IAudioPlayer? player = null,
        IVideoExporter? exporter = null,
        ExportPresetStore? presetStore = null,
        IVideoEncoderDetector? encoderDetector = null)
    {
        _store = store ?? new AppSettingsStore();
        _loader = loader ?? new AudioLoader();
        _player = player ?? new AudioPlayer();

        // Зонд один на панель и выгрузку: обеим нужен один и тот же ответ про видеокарту, а
        // каждая проба - это запуски ffmpeg.
        var detectors = encoderDetector ?? new VideoEncoderDetector();
        _exporter = exporter ?? new Mp4Exporter(encoderDetector: detectors);

        var stored = _store.Load();

        Settings = new SettingsViewModel(stored, presetStore, detectors);
        Settings.Changed += OnSettingsChanged;

        // Эквалайзер живёт в плеере, и полосы приходят в него при каждой правке. При запуске
        // правки ещё не было, поэтому полосы задаются один раз здесь.
        var initial = Settings.ToAppSettings().Spectrum;
        _player.SetEqualizer(initial.Equalizer, initial.ApplyEqualizerToAudio);

        // Громкость ставится полю напрямую, а не свойством: при загрузке она не правка
        // пользователя, и отложенная запись из-за неё стартовать не должна. В плеер она
        // идёт синхронно: иначе открытый после запуска трек звучал бы громче, чем стоит
        // ползунок, до первого его движения.
        _volume = stored.Volume;
        _player.SetVolume(_volume);

        // Прежняя громкость для кнопки «включить звук» берётся из файла: в файле ноль -
        // восстанавливать нечего, и тогда возвращается полная.
        _previousVolume = stored.Volume > 0d ? stored.Volume : 1d;
        _lastVolume = stored.Volume;
        _volumeBeforeGesture = _previousVolume;

        PreviewWidth = Settings.ExportWidth;
        PreviewHeight = Settings.ExportHeight;
        UpdatePreview();
    }

    public SettingsViewModel Settings { get; }

    /// <summary>Кадр, который рисует SpectrumElement. Меняется целиком, частичного обновления нет.</summary>
    [ObservableProperty] private SpectrumSnapshot _snapshot = SpectrumSnapshot.Empty;

    [ObservableProperty] private string _fileTitle = "Файл не выбран";

    [ObservableProperty] private string _status = "Откройте аудиофайл, чтобы увидеть спектр.";

    /// <summary>Ошибка в строке статуса: она красится красным, пока не придёт обычное сообщение.</summary>
    [ObservableProperty] private bool _isStatusError;

    /// <summary>
    /// Подробности сообщения для подсказки на строке статуса. В самой строке только заголовок:
    /// слова ffmpeg и команда запуска обрезались бы многоточием, поэтому полный текст живёт
    /// здесь и в окне. У обычных сообщений подробностей нет, и строка пустая.
    /// </summary>
    [ObservableProperty] private string _statusDetail = string.Empty;

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private double _previewWidth;

    [ObservableProperty] private double _previewHeight;

    [ObservableProperty] private TimeSpan _duration;

    [ObservableProperty] private bool _isPlaying;

    [ObservableProperty] private bool _hasTrack;

    /// <summary>Идёт выгрузка видео: кнопки экспорта блокируются, а появляется прогресс.</summary>
    [ObservableProperty] private bool _isExporting;

    /// <summary>Доля выгруженных кадров от 0 до 1.</summary>
    [ObservableProperty] private double _exportProgress;

    public bool CanExport => HasTrack && !IsExporting;

    /// <summary>Ход выгрузки словами: в процентах проценты не нужны, нужен остаток времени.</summary>
    public string ExportProgressText
    {
        get
        {
            if (!IsExporting)
            {
                return string.Empty;
            }

            if (_isCancelling)
            {
                // Отмена уже нажата, но ffmpeg ещё добивает очередной кадр: полоса перестаёт
                // расти, а надпись перестаёт вводить в заблуждение.
                return "Отмена...";
            }

            if (_progress is null)
            {
                return "Подготовка кадров...";
            }

            var percent = (int)(_progress.Fraction * 100d);
            var remaining = _progress.Remaining is { } left ? $", осталось {left:hh\\:mm\\:ss}" : string.Empty;

            return $"Кадр {_progress.FramesDone} из {_progress.TotalFrames}, {percent}%{remaining}";
        }
    }

    /// <summary>Позиция в секундах. Публикуется окну через <see cref="PositionChanged"/>.</summary>
    [ObservableProperty] private double _positionSeconds;

    /// <summary>
    /// Громкость прослушивания от нуля до единицы. Меняют её ползунок, кнопка отключения
    /// и сброс настроек.
    /// </summary>
    [ObservableProperty] private double _volume;

    /// <summary>Громкость до отключения звука: при возврате должна встать та же.</summary>
    private double _previousVolume = 1d;

    /// <summary>
    /// Громкость, с которой человек уводит ползунок в ноль. Пока громкость падает, она
    /// остаётся прежней: ползунок идёт к нулю десятками промежуточных значений, и «последняя
    /// ненулевая» после такого жеста - это около нуля, а вернуть надо тот уровень, который
    /// был у человека.
    /// </summary>
    private double _volumeBeforeGesture;

    /// <summary>Предыдущее значение громкости: по нему видно, что громкость выросла.</summary>
    private double _lastVolume;

    /// <summary>
    /// Включает ползунок громкости, а не убавляется до нуля: ноль хранит сам ползунок, и
    /// зависшего состояния «звук тихо, но ползунок сбоку» не бывает.
    /// </summary>
    public bool IsMuted => Volume <= 0d;

    /// <summary>
    /// Подсказка ползунка громкости: значение в целых процентах, от нуля до ста. Проценты
    /// целые потому ползунок и в процентах: тик 0,01 - это ровно один процент, а шумы
    /// умножения снимаются округлением.
    /// </summary>
    public string VolumeLabel => $"Громкость: {Math.Round(Volume * 100d):0}%";

    /// <summary>
    /// Окно подписывается на позицию и само выставляет её ползунку.
    /// </summary>
    /// <remarks>
    /// Привязка <c>Value</c> здесь не годится: пока пользователь тянет бегунок, источник
    /// обновляется каждые 16 мс и затирает его значение прежней позицией. Через подписку
    /// обновление приходит только тогда, когда ползунком никто не управляет.
    /// </remarks>
    public Action<double>? PositionChanged { get; set; }

    /// <summary>
    /// Сообщение для показа окном: и об ошибке, и об удачной выгрузке. Заголовок и текст
    /// разведены: заголовок короткий и в строке статуса остаётся он, а текст бывает
    /// на пол-экрана, и в строке статуса он обрезается.
    /// </summary>
    public Action<Notice>? NoticeRequested { get; set; }

    /// <summary>
    /// Пишет сообщение в строку статуса и сразу помечает его ошибкой или нет. Через один
    /// метод, а не присваиванием напрямую: обычное сообщение обязано гасить красный цвет
    /// предыдущей ошибки, а забытое присваивание оставило бы её навсегда.
    /// </summary>
    /// <param name="text">Текст строки. У ошибки это только заголовок.</param>
    /// <param name="isError">Ошибка ли это: строка красится красным.</param>
    /// <param name="detail">
    /// Подробности для подсказки. Снимается на каждом сообщении, иначе подсказка от прежней
    /// ошибки осталась бы висеть под обычным текстом.
    /// </param>
    /// <summary>
    /// Чем кодировалось — в подсказку строки статуса. При обычном программном кодировании
    /// пусто: сообщать о том, что и так выбрано, незачем, и подсказка остаётся для деталей.
    /// </summary>
    private static string DescribeEncoder(VideoExportResult result)
    {
        var notes = new List<string>();

        if (!string.IsNullOrEmpty(result.Encoder))
        {
            notes.Add($"Кодировала видеокарта ({result.Encoder}).");
        }
        else if (result.FellBackToCpu)
        {
            notes.Add("Кодировал процессор: видеокарта не нашлась или не умеет выбранный кодек.");
        }

        if (result.FellBackToMono)
        {
            // Исходник не прочитался, и звук ролика собран из моно-микса: он тише на 3 дБ
            // и без пространства. Молчать об этом нельзя - ролик вышел другим.
            notes.Add("Звук ролика вышел моно: исходник не удалось прочитать заново.");
        }

        return string.Join(Environment.NewLine, notes);
    }

    private void SetStatus(string text, bool isError = false, string? detail = null)
    {
        Status = text;
        IsStatusError = isError;
        StatusDetail = detail ?? string.Empty;
    }

    /// <summary>
    /// Собирает отчёт об ошибке и отдаёт его в строку статуса и окну. В строку идёт только
    /// заголовок: подробности про ffmpeg и команду запуска обрезались бы многоточием, и после
    /// закрытия окна осталось бы нечитаемое «Не удалось выгрузить видео: ffmpeg vers...».
    /// Полный текст остаётся в подсказке строки и в самом окне.
    /// </summary>
    private void ReportError(string title, Exception exception)
    {
        var notice = new Notice(title, exception.Message);

        SetStatus(notice.Title, isError: true, notice.Message);

        // Все четыре места вызова живут на потоке интерфейса: два в обработчиках команд,
        // два в продолжениях await, которые возвращаются на SynchronizationContext окна.
        // Диспетчеризации здесь нет намеренно: вызов из фонового потока упал бы громко,
        // а тихая обвязка скрыла бы ошибку.
        NoticeRequested?.Invoke(notice);
    }

    public string TimeLabel => $"{TimeSpan.FromSeconds(PositionSeconds):mm\\:ss} / {Duration:mm\\:ss}";

    /// <summary>
    /// Убирает звук совсем и возвращает прежнюю громкость. Громкость до отключения хранится
    /// в поле: восстановить на максимум наугад хуже, чем вернуть то, что было. Ноль после
    /// запуска считается правкой пользователя, а не состоянием «звук был выключен».
    /// </summary>
    [RelayCommand]
    private void ToggleMute()
    {
        if (Volume > 0d)
        {
            var muted = Volume;

            // Обнуление идёт через свойство, а переход в ноль переписывает память о прежней
            // громкости: след от жеста над ползунком. Кнопка знает громкость точно, и её
            // значение важнее следа.
            Volume = 0d;
            _previousVolume = muted;

            return;
        }

        Volume = _previousVolume > 0d ? _previousVolume : 1d;
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Открыть аудиофайл",
            Filter = AudioFileFilter
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadFileAsync(dialog.FileName);
        }
    }

    /// <summary>
    /// Пишет настройки в файл. Кнопки в панели нет: то же самое происходит при выходе из
    /// окна, и отдельная кнопка только перезаписывала бы файл вручную.
    /// </summary>
    [RelayCommand]
    private void SaveSettings()
    {
        // Громкость подставляется здесь, а не в панели: SettingsViewModel её не правит, и
        // перестройка источника кадров на её изменение запускаться не должна. Через
        // with она попадает в файл вместе с остальными полями.
        if (_store.Save(Settings.ToAppSettings() with { Volume = Volume }, out var failure))
        {
            _saveFailed = false;

            return;
        }

        // Отказ повторялся бы на каждой правке и затирал бы сведения о треке, поэтому текст
        // появляется только на переходе «работало → сломалось». Об успехе не сообщаем вовсе:
        // это обычный порядок вещей.
        //
        // Здесь причина остаётся в строке, в отличие от остальных ошибок: окна у этого отказа
        // нет, а «Не удалось сохранить настройки» без причины ни о чём не говорит.
        if (!_saveFailed)
        {
            SetStatus("Не удалось сохранить настройки: " + failure, isError: true);
        }

        _saveFailed = true;
    }

    /// <summary>
    /// Ставит отложенную запись. Правки идут десятками раз в секунду, а перезаписывать файл
    /// на каждое движение ползунка нельзя. Пауза отсчитывается заново при каждой правке,
    /// поэтому жест ползунка даёт одну запись в конце, а не одну на каждое событие.
    /// </summary>
    private void ScheduleSave()
    {
        SavePending = true;

        _saveTimer ??= CreateSaveTimer();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>
    /// Пишет настройки, если правки ждали своей очереди. Очередь снимается до записи: иначе
    /// неудачная попытка осталась бы висеть и повторялась бы на каждом щелчке таймера.
    /// </summary>
    internal void FlushSave()
    {
        if (!SavePending)
        {
            return;
        }

        SavePending = false;
        SaveSettings();
    }

    private DispatcherTimer CreateSaveTimer()
    {
        // Background, а не Render как у превью: запись двух килобайт не должна отбирать кадры
        // у столбиков.
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = SaveDelay };
        timer.Tick += (_, _) => FlushSave();

        return timer;
    }

    [RelayCommand]
    private void ResetSettings()
    {
        var defaults = SettingsNormalizer.Normalize(new AppSettings());

        Settings.Apply(defaults);

        // Громкость живёт в модели, а не в панели, поэтому сброс панели её не трогал: после
        // сброса звук мог остаться выключенным, хотя кнопка обещает «сбросить все настройки».
        Volume = defaults.Volume;
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            Pause();
            return;
        }

        Play();
    }

    [RelayCommand]
    private void StopPlayback()
    {
        _player.StopPlayback();
        IsPlaying = false;
        SetPosition(0d);
        StopTimer();
        UpdatePreview();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportVideo()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Сохранить видео",
            Filter = VideoFileFilter,
            DefaultExt = ".mp4",
            AddExtension = true,
            FileName = _audioPath is null ? "видео.mp4" : Path.GetFileNameWithoutExtension(_audioPath) + ".mp4"
        };

        if (dialog.ShowDialog() == true)
        {
            await ExportVideoAsync(dialog.FileName);
        }
    }

    [RelayCommand(CanExecute = nameof(IsExporting))]
    private void CancelExport()
    {
        _isCancelling = true;

        try
        {
            _exportCancellation?.Cancel();
        }
        catch (AggregateException)
        {
            // Отмена подписки не должна ронять приложение из обработчика команды: пользователь
            // уже получил то, чего хотел, остальное - частности уборки.
        }
    }

    /// <summary>
    /// Выгружает видео в <paramref name="outputPath"/>. Диалог сохранения живёт в команде,
    /// а сама выгрузка - здесь, чтобы её можно было проверить тестом без диалога.
    /// </summary>
    public async Task ExportVideoAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (_audio is null)
        {
            SetStatus("Сначала откройте аудиофайл.");
            return;
        }

        // Рендер идёт на всех ядрах и занимает UI: звук на том же треке мешал бы и себе,
        // и выгрузке, поэтому воспроизведение на время выгрузки останавливается.
        Pause();
        StopTimer();

        // Исходник нужен выгрузке для звука с каналами: моно-микс, на котором построен
        // спектр, тише на 3 дБ и без пространства.
        var request = new VideoExportRequest(_audio, Settings.ToAppSettings(), outputPath, _audioPath);

        // Источник отмены живёт в локальной переменной, а поле нужно только кнопке «отмена».
        // При закрытии окна поле обнуляется, и обращение к нему в конце выгрузки упало бы.
        using var cancellation = new CancellationTokenSource();

        _exportCancellation = cancellation;
        _isCancelling = false;
        _progress = null;
        ExportProgress = 0d;
        IsExporting = true;
        IsBusy = true;
        SetStatus($"Выгрузка {Path.GetFileName(outputPath)}...");

        try
        {
            var result = await _exporter.ExportAsync(
                request,
                new Progress<VideoExportProgress>(OnExportProgress),
                cancellation.Token);

            if (result.Cancelled)
            {
                SetStatus("Выгрузка отменена.");
                return;
            }

            ExportProgress = 1d;

            // Чем кодировалось, в строку статуса не влезает: она и так переполнена именем
            // файла, и текст обрежется многоточием. Подсказка на строке — правильное место.
            // Отход от видеокарты на процессор говорится отдельно: иначе настройка молча не
            // сделала того, о чём просили, и это видно только по времени выгрузки.
            SetStatus(
                $"Готово: {result.Frames} кадров за {result.Elapsed:mm\\:ss}, {Path.GetFileName(result.Path)}",
                detail: DescribeEncoder(result));

            // Окно об успехе тоже легко пропустить: строка статуса одна, она мелькает и
            // остаётся в углу. Но галочка «тихая выгрузка» его отключает - кому не надо
            // подтверждать готовность каждый раз.
            if (!Settings.QuietExport)
            {
                NoticeRequested?.Invoke(new Notice(
                    "Выгрузка готова",
                    $"{Status}{Environment.NewLine}{Environment.NewLine}{result.Path}",
                    Path.GetDirectoryName(result.Path)));
            }
        }
        catch (Exception exception)
        {
            ReportError("Не удалось выгрузить видео", exception);
        }
        finally
        {
            if (ReferenceEquals(_exportCancellation, cancellation))
            {
                _exportCancellation = null;
            }

            IsExporting = false;
            IsBusy = false;
        }
    }

    public async Task LoadFileAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        IsBusy = true;
        try
        {
            var audio = await _loader.LoadAsync(path);

            _player.StopPlayback();
            StopTimer();

            _audio = audio;
            _audioPath = path;
            _frames = CreateFrameSource(audio);

            FileTitle = Path.GetFileName(path);
            Duration = audio.Duration;
            SetPosition(0d);
            IsPlaying = false;
            HasTrack = true;
            SetStatus($"{FileTitle}: {audio.Duration:mm\\:ss}, {audio.SampleRate} Гц, каналы сведены в моно");
            UpdatePreview();
        }
        catch (Exception exception) when (exception is AudioDecodeException or FileNotFoundException or IOException)
        {
            FileTitle = "Файл не выбран";
            ReportError("Не удалось открыть файл", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Пользователь начал тянуть ползунок позиции: до отпускания таймер позицию не трогает.
    /// </summary>
    /// <remarks>
    /// Вызывается окном по изменению значения ползунка, а не по событию нажатия мыши:
    /// нажатие до ползунка не доходит, а значение меняется всегда, когда им пользуется человек.
    /// </remarks>
    public void BeginScrub() => _isScrubbing = true;

    public void EndScrub(double seconds)
    {
        _isScrubbing = false;
        Seek(seconds);
    }

    /// <summary>
    /// Запоминает громкость в тот момент, когда человек взялся за ползунок.
    /// </summary>
    /// <remarks>
    /// Вызывается окном: момент взятия снаружи модели не виден, а громкость к этому времени
    /// могла долго стоять на одном уровне, и «последний подъём» был давным-давно.
    /// </remarks>
    public void RememberVolume() => _volumeBeforeGesture = Volume;

    /// <summary>
    /// Пересчитывает кадр по текущей позиции воспроизведения. Запускается таймером; вынесено
    /// отдельно, чтобы ту же логику можно было проверить тестом без звуковой карты.
    /// </summary>
    public void UpdatePlayback()
    {
        if (_audio is null)
        {
            return;
        }

        var position = _player.Position;

        if (position >= _audio.Duration)
        {
            // Трек доигран: возвращаемся в начало, как это делает обычный проигрыватель.
            _player.StopPlayback();
            IsPlaying = false;
            StopTimer();
            SetPosition(0d);
            UpdatePreview();
            return;
        }

        if (!_isScrubbing)
        {
            SetPosition(position.TotalSeconds);
        }

        UpdatePreview(position);
    }

    partial void OnPositionSecondsChanged(double value) => OnPropertyChanged(nameof(TimeLabel));

    partial void OnDurationChanged(TimeSpan value) => OnPropertyChanged(nameof(TimeLabel));

    partial void OnVolumeChanged(double value)
    {
        // У открытого трека громкость меняется сразу; при закрытом плеер запомнит её для
        // следующего Play. Запись отложенная: правка идёт на каждый шаг ползунка, файл на
        // каждый шаг переписывать нельзя.
        _player.SetVolume(value);
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(VolumeLabel));
        ScheduleSave();

        var previous = _lastVolume;
        _lastVolume = value;

        // Громкость выросла - значит, человек поднял её своими руками, и с этого уровня он
        // и пойдёт вниз, если заглушит звук. Пока громкость падает, уровень остаётся
        // прежним: ползунок идёт к нулю десятками промежуточных значений.
        if (value > previous)
        {
            _volumeBeforeGesture = value;
        }

        // Ноль на ползунке - это выключенный звук, и кнопка «включить звук» должна вернуть
        // тот уровень, с которого человек уводил ползунок в ноль, а не последний ненулевой.
        if (value <= 0d && _volumeBeforeGesture > 0d)
        {
            _previousVolume = _volumeBeforeGesture;
        }
    }

    private void Play()
    {
        if (_audio is null || _audioPath is null)
        {
            return;
        }

        // Нажатие «play» снимает перемотку, даже если отпускание мыши пропало за пределами
        // окна: иначе флаг перемотки висел бы вечно и таймер перестал бы двигать позицию.
        _isScrubbing = false;

        try
        {
            _player.Play(_audioPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Формат мог быть декодирован через ffmpeg, а NAudio его не играет: об этом честно
            // сообщаем и в строке статуса, и окном, а не молча оставляем кнопку включённой.
            ReportError("Не удалось включить воспроизведение", exception);
            return;
        }

        IsPlaying = true;
        StartTimer();
        UpdatePlayback();
    }

    private void Pause()
    {
        _player.Pause();
        IsPlaying = false;
        StopTimer();
    }

    private void Seek(double seconds)
    {
        if (_audio is null)
        {
            return;
        }

        var total = _audio.Duration.TotalSeconds;
        var target = TimeSpan.FromSeconds(Math.Clamp(seconds, 0d, total));

        // Перезапуск вывода нужен, только если позиция действительно изменилась: иначе клик
        // по самому бегунку дал бы щелчок ради перехода в ту же точку.
        if (Math.Abs(target.TotalSeconds - PositionSeconds) < MinimumSeekDeltaSeconds)
        {
            return;
        }

        // Исключение из обработчика мыши убивает приложение, поэтому здесь оно становится
        // строкой в статусе.
        try
        {
            _player.Seek(target);
        }
        catch (Exception exception)
        {
            ReportError("Ошибка перемотки", exception);

            return;
        }

        SetPosition(target.TotalSeconds);
        UpdatePreview(target);
    }

    /// <summary>
    /// Обновляет позицию и отдаёт её окну. Вызывается только когда ползунком никто не
    /// управляет, иначе значение, которое выбрал пользователь, было бы затёрто.
    /// </summary>
    private void SetPosition(double seconds)
    {
        PositionSeconds = seconds;
        PositionChanged?.Invoke(seconds);
    }

    private void StartTimer()
    {
        _timer ??= CreateTimer();

        // Частота кадров превью берётся из настроек экспорта: картинка на экране и в mp4
        // обновляется с одинаковой частотой.
        var framesPerSecond = Math.Clamp(Settings.ExportFramesPerSecond, 1, MaximumFramesPerSecond);
        _timer.Interval = TimeSpan.FromSeconds(1d / framesPerSecond);

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void StopTimer()
    {
        if (_timer is not null)
        {
            _timer.Stop();
        }
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => UpdatePlayback();

        return timer;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Запись отложенная: правка приходит на каждое движение ползунка, а файл на то
        // движение перезаписывать нельзя. При выходе из окна очередь всё равно допишется.
        ScheduleSave();

        var settings = SettingsNormalizer.Normalize(Settings.ToAppSettings());

        // Полосы эквалайзера едут в плеер сразу: файл при этом не переоткрывается, и
        // положение звука не сбивается.
        _player.SetEqualizer(settings.Spectrum.Equalizer, settings.Spectrum.ApplyEqualizerToAudio);

        PreviewWidth = settings.Export.Width;
        PreviewHeight = settings.Export.Height;

        if (_audio is not null)
        {
            // Состояние переносится в новый источник: сглаживание, а если наполнение кадра не
            // изменилось - и сам кадр. Без переноса столбики сдвинулись бы на шаг
            // сглаживания при каждой правке, а правки оформления двигали бы их вообще.
            var rebuilt = new SpectrumFrameSource(_audio, settings.Spectrum, BarCount(settings));

            if (_frames is not null)
            {
                rebuilt.SeedFrom(_frames);
            }

            _frames = rebuilt;
        }

        // Позиция берётся у плеера, а не по умолчанию: кадр без позиции рисует начало
        // трека, и столбики возвращались бы к нему на каждое движение ползунка настроек.
        UpdatePreview(_player.Position);
    }

    /// <summary>
    /// Пересчитывает кадр под заданный момент трека. Превью и выгрузка идут через один и тот же
    /// <see cref="SpectrumFrameSource"/> и по одним и тем же сэмплам, иначе картинка в mp4
    /// разошлась бы с тем, что видно на экране.
    /// </summary>
    private void UpdatePreview(TimeSpan? position = null)
    {
        var settings = SettingsNormalizer.Normalize(Settings.ToAppSettings());
        var bars = settings.Bars;

        var values = _frames is null
            ? new float[BarCount(settings)]
            : _frames.GetBars(position ?? TimeSpan.Zero);

        // Частота кадров идёт в снимок из настроек экспорта: след затухает по времени, а таймер
        // превью бьёт с той же частотой, поэтому длина следа в превью и в ролике совпадает.
        Snapshot = new SpectrumSnapshot(values, settings.Colors, bars, settings.Export.FramesPerSecond);
    }

    /// <summary>
    /// Столько значений, сколько столбиков влезет в кадр настроек: превью и видео одного
    /// размера, поэтому и число столбиков у них одно.
    /// </summary>
    private int BarCount(AppSettings settings) => new BarLayout(PreviewWidth, PreviewHeight, settings.Bars).BarCount;

    private SpectrumFrameSource CreateFrameSource(AudioData audio)
        => new(audio, SettingsNormalizer.Normalize(Settings.ToAppSettings()).Spectrum, BarCount(SettingsNormalizer.Normalize(Settings.ToAppSettings())));

    private void OnExportProgress(VideoExportProgress progress)
    {
        _progress = progress;
        ExportProgress = progress.Fraction;
    }

    partial void OnIsExportingChanged(bool value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
        ExportVideoCommand.NotifyCanExecuteChanged();
        CancelExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnExportProgressChanged(double value) => OnPropertyChanged(nameof(ExportProgressText));

    partial void OnHasTrackChanged(bool value) => ExportVideoCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Вызывается при закрытии окна. Выгрузка отменяется, а не просто забывается:
    /// иначе вместе с интерфейсом останутся процесс ffmpeg и поток рендера.
    /// </summary>
    public void Dispose()
    {
        _exportCancellation?.Cancel();
        _exportCancellation?.Dispose();
        _exportCancellation = null;

        StopTimer();

        if (_saveTimer is not null)
        {
            _saveTimer.Stop();
        }

        _player.Dispose();

        GC.SuppressFinalize(this);
    }
}
