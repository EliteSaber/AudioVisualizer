using System.IO;
using AudioVisualizer.Core.Analysis;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AudioVisualizer.App.ViewModels;

/// <summary>Состояние окна: открытый файл, кадр превью и панель настроек.</summary>
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

    private readonly AppSettingsStore _store;
    private readonly AudioLoader _loader;

    private AudioData? _audio;
    private SpectrumPipeline? _pipeline;

    public MainViewModel(AppSettingsStore? store = null, AudioLoader? loader = null)
    {
        _store = store ?? new AppSettingsStore();
        _loader = loader ?? new AudioLoader();

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

    public async Task LoadFileAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        IsBusy = true;
        try
        {
            var audio = await _loader.LoadAsync(path);

            _audio = audio;
            _pipeline = new SpectrumPipeline(Settings.ToAppSettings().Spectrum, audio.SampleRate);

            FileTitle = Path.GetFileName(path);
            Duration = audio.Duration;
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
    /// Пересчитывает кадр под текущий момент трека. Превью и экспорт идут через один и тот же
    /// <see cref="SpectrumPipeline"/>, иначе картинка в mp4 разошлась бы с тем, что видно на экране.
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