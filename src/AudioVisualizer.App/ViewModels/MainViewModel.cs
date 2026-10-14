using System.IO;
using System.Windows.Threading;
using AudioVisualizer.Core.Analysis;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AudioVisualizer.App.ViewModels;

/// <summary>
/// Состояние окна: открытый файл, воспроизведение, кадр превью и панель настроек.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
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

    private const int MaximumFramesPerSecond = 60;

    /// <summary>Меньше этой разницы перемотка считается щелчком по бегунку без переноса.</summary>
    private const double MinimumSeekDeltaSeconds = 0.05d;

    private readonly AppSettingsStore _store;
    private readonly AudioLoader _loader;
    private readonly IAudioPlayer _player;

    private AudioData? _audio;
    private SpectrumPipeline? _pipeline;
    private string? _audioPath;
    private DispatcherTimer? _timer;
    private bool _isScrubbing;

    public MainViewModel(AppSettingsStore? store = null, AudioLoader? loader = null, IAudioPlayer? player = null)
    {
        _store = store ?? new AppSettingsStore();
        _loader = loader ?? new AudioLoader();
        _player = player ?? new AudioPlayer();

        Settings = new SettingsViewModel(_store.Load());
        Settings.Changed += OnSettingsChanged;

        PreviewWidth = Settings.ExportWidth;
        PreviewHeight = Settings.ExportHeight;
        UpdatePreview();
    }

    public SettingsViewModel Settings { get; }

    /// <summary>Кадр, который рисует SpectrumElement. Меняется целиком, частичного обновления нет.</summary>
    [ObservableProperty] private SpectrumSnapshot _snapshot = SpectrumSnapshot.Empty;

    [ObservableProperty] private string _fileTitle = "Файл не выбран";

    [ObservableProperty] private string _status = "Откройте аудиофайл, чтобы увидеть спектр.";

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private double _previewWidth;

    [ObservableProperty] private double _previewHeight;

    [ObservableProperty] private TimeSpan _duration;

    [ObservableProperty] private bool _isPlaying;

    [ObservableProperty] private bool _hasTrack;

    /// <summary>Позиция в секундах. Публикуется окну через <see cref="PositionChanged"/>.</summary>
    [ObservableProperty] private double _positionSeconds;

    /// <summary>
    /// Окно подписывается на позицию и само выставляет её ползунку.
    /// </summary>
    /// <remarks>
    /// Привязка <c>Value</c> здесь не годится: пока пользователь тянет бегунок, источник
    /// обновляется каждые 16 мс и затирает его значение прежней позицией. Через подписку
    /// обновление приходит только тогда, когда ползунком никто не управляет.
    /// </remarks>
    public Action<double>? PositionChanged { get; set; }

    public string TimeLabel => $"{TimeSpan.FromSeconds(PositionSeconds):mm\\:ss} / {Duration:mm\\:ss}";

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

    [RelayCommand]
    private void SaveSettings() => _store.Save(Settings.ToAppSettings());

    [RelayCommand]
    private void ResetSettings() => Settings.Apply(SettingsNormalizer.Normalize(new AppSettings()));

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
            _pipeline = new SpectrumPipeline(Settings.ToAppSettings().Spectrum, audio.SampleRate);

            FileTitle = Path.GetFileName(path);
            Duration = audio.Duration;
            SetPosition(0d);
            IsPlaying = false;
            HasTrack = true;
            Status = $"{FileTitle}: {audio.Duration:mm\\:ss}, {audio.SampleRate} Гц, каналы сведены в моно";
            UpdatePreview();
        }
        catch (Exception exception) when (exception is AudioDecodeException or FileNotFoundException or IOException)
        {
            FileTitle = "Файл не выбран";
            Status = "Не удалось открыть файл: " + exception.Message;
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
            _player.Play(_audioPath, volume: 1d);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Формат мог быть декодирован через ffmpeg, а NAudio его не играет: об этом честно
            // сообщаем в строке статуса, а не молча оставляем кнопку включённой.
            Status = "Не удалось включить воспроизведение: " + exception.Message;
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
            Status = "Ошибка перемотки: " + exception.Message;

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
        var settings = SettingsNormalizer.Normalize(Settings.ToAppSettings());

        PreviewWidth = settings.Export.Width;
        PreviewHeight = settings.Export.Height;

        if (_audio is not null)
        {
            _pipeline = new SpectrumPipeline(settings.Spectrum, _audio.SampleRate);
        }

        UpdatePreview();
    }

    /// <summary>
    /// Пересчитывает кадр под заданный момент трека. Превью и экспорт идут через один и тот же
    /// <see cref="SpectrumPipeline"/> и по одним и тем же сэмплам, иначе картинка в mp4
    /// разошлась бы с тем, что видно на экране.
    /// </summary>
    private void UpdatePreview(TimeSpan? position = null)
    {
        var settings = SettingsNormalizer.Normalize(Settings.ToAppSettings());
        var bars = settings.Bars;
        var layout = new BarLayout(PreviewWidth, PreviewHeight, bars);

        var values = new float[layout.BarCount];

        if (_audio is not null && _pipeline is not null)
        {
            var startIndex = (int)((position ?? TimeSpan.Zero).TotalSeconds * _audio.SampleRate);
            values = _pipeline.AnalyzeBars(_audio.Samples, startIndex, layout.BarCount);
        }

        Snapshot = new SpectrumSnapshot(values, settings.Colors, bars);
    }
}