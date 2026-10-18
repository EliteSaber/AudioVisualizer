using System.Windows.Controls;
using System.Windows.Data;
using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;
using AudioVisualizer.Tests.Export;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Список выбора кодировщиков пересобирается на ходу, и это ломало выбор.
/// </summary>
/// <remarks>
/// Регрессия: при очистке коллекции <see cref="ComboBox"/> приводит SelectedValue в null, а
/// двусторонняя привязка записывает этот null в настройку. Поле оставалось пустым, а выбранный
/// кодировщик не переживал даже перезапуск: нормализация читала null и ставила «программно».
/// Прочие списки так не ведут себя потому, что статичны и не очищаются, поэтому ловушку видели
/// только на этом списке — и видели глазами, а не тестом.
/// </remarks>
/// <remarks>
/// Проверка настоящая: <see cref="ComboBox"/> привязан к настоящей
/// <see cref="SettingsViewModel"/>, а копия логики пересборки в тесте проверяла бы саму себя и
/// прошла бы в любом случае, включая сломанную.
/// </remarks>
[Collection(WpfUi.Name)]
public class EncoderChoiceBindingTests
{
    [Fact]
    public void ПробаВидеокарты_НеОпустощаетПоле() => Sta.Run(() =>
    {
        var viewModel = CreateViewModel();
        var comboBox = Bind(viewModel);

        viewModel.RefreshEncodersAsync().GetAwaiter().GetResult();

        Assert.Equal(ExportSettings.DefaultVideoEncoder, viewModel.VideoEncoder);
        Assert.Equal(ExportSettings.DefaultVideoEncoder, comboBox.SelectedValue);
        Assert.Equal(
            ExportSettings.DefaultVideoEncoder,
            (comboBox.SelectedItem as Choice<string>)?.Value);
    });

    [Fact]
    public void ВыбранныйКодировщик_ПереживаетПробу() => Sta.Run(() =>
    {
        var viewModel = CreateViewModel(
            new AppSettings
            {
                Export = new ExportSettings { VideoEncoder = "nvenc", VideoCodec = "libx265" }
            },
            new FakeEncoderDetector([new VideoEncoderSupport("nvenc", "NVIDIA", "hevc_nvenc")]));

        var comboBox = Bind(viewModel);

        viewModel.RefreshEncodersAsync().GetAwaiter().GetResult();

        Assert.Equal("nvenc", viewModel.VideoEncoder);
        Assert.Equal("nvenc", comboBox.SelectedValue);
        Assert.Equal("NVIDIA", (comboBox.SelectedItem as Choice<string>)?.ToString());
    });

    [Fact]
    public void ВыборПользователя_ДоживаетДоНастроек() => Sta.Run(() =>
    {
        var viewModel = CreateViewModel();
        var comboBox = Bind(viewModel);

        // Настоящее действие человека: выбрал пункт из списка, а не присвоил свойству из кода.
        comboBox.SelectedIndex = 1;

        Assert.Equal(ExportSettings.AutoVideoEncoder, viewModel.VideoEncoder);
        Assert.Equal(ExportSettings.AutoVideoEncoder, viewModel.ToAppSettings().Export.VideoEncoder);
    });

    [Fact]
    public void ПустоеЗначение_НеДоходитДоНастроек() => Sta.Run(() =>
    {
        var viewModel = CreateViewModel();

        // Кто-то снаружи обнулил настройку — в файл такое уходить не должно: нормализация
        // молча превратила бы его в «программно», и выбранный кодировщик потерялся бы навсегда.
        viewModel.VideoEncoder = null!;

        Assert.Equal(ExportSettings.DefaultVideoEncoder, viewModel.ToAppSettings().Export.VideoEncoder);
    });

    /// <summary>
    /// Регрессия: выбранный пресет выгрузки снимался при открытии вкладки «Экспорт».
    /// </summary>
    /// <remarks>
    /// Проба пересобирает список кодировщиков, а <see cref="ComboBox"/> на Clear() приводит
    /// SelectedValue в null, и двусторонняя привязка пишет этот null в настройку. Панель этот
    /// null тут же возвращает, но к тому моменту, как событие о правке дошло до сброса выбора
    /// пресета, тот уже был снят. Симптом был такой: параметры ролика остались от пресета,
    /// а в списке не было выбрано ничего.
    /// </remarks>
    [Fact]
    public void ПробаВидеокарты_НеСнимаетВыбранныйПресет() => Sta.Run(() =>
    {
        var viewModel = CreateViewModel();
        Bind(viewModel);

        var preset = viewModel.ExportPresets.First(item => item.Name == "720p30");
        viewModel.SelectedExportPreset = preset;

        viewModel.RefreshEncodersAsync().GetAwaiter().GetResult();

        Assert.Same(preset, viewModel.SelectedExportPreset);
    });

    /// <summary>
    /// То же самое с другой стороны: список пресетов привязан к тому же свойству и после пробы
    /// обязан показывать выбранный пункт, а не пустое поле.
    /// </summary>
    [Fact]
    public void СписокПресета_ПоказываетВыборПослеПробы() => Sta.Run(() =>
    {
        var viewModel = CreateViewModel();
        Bind(viewModel);
        var presets = BindPresets(viewModel);

        var preset = viewModel.ExportPresets.First(item => item.Name == "720p30");

        // Настоящее действие человека: выбрал пункт из списка, а не присвоил свойство из кода.
        presets.SelectedItem = preset;
        Assert.Same(preset, viewModel.SelectedExportPreset);

        viewModel.RefreshEncodersAsync().GetAwaiter().GetResult();

        Assert.Same(preset, viewModel.SelectedExportPreset);
        Assert.Same(preset, presets.SelectedItem);
    });

    /// <summary>Подпись списка подсказывает, что в нём нашлось: без карты текст должен быть виден.</summary>
    [Fact]
    public void ПодсказкаВиднаТамГдеЕстьОЧемСказать() => Sta.Run(() =>
    {
        var found = CreateViewModel(
            settings: null,
            new FakeEncoderDetector([new VideoEncoderSupport("nvenc", "NVIDIA", "h264_nvenc")]));

        found.RefreshEncodersAsync().GetAwaiter().GetResult();
        Assert.False(found.HasEncodersMessage);

        var missing = CreateViewModel(settings: null, new FakeEncoderDetector([]));
        missing.RefreshEncodersAsync().GetAwaiter().GetResult();
        Assert.True(missing.HasEncodersMessage);
    });

    private static SettingsViewModel CreateViewModel(AppSettings? settings = null, IVideoEncoderDetector? detector = null)
    {
        var presetsPath = Path.Combine(Path.GetTempPath(), "av-test-encoderbinding.json");

        if (File.Exists(presetsPath))
        {
            File.Delete(presetsPath);
        }

        return new SettingsViewModel(
            settings ?? AppSettings.Default,
            new ExportPresetStore(presetsPath),
            detector ?? new FakeEncoderDetector([]));
    }

    /// <summary>Тот самый список из разметки: значение выбирается по пути в пункте.</summary>
    private static ComboBox Bind(SettingsViewModel viewModel)
    {
        var comboBox = new ComboBox
        {
            SelectedValuePath = "Value",
            ItemsSource = viewModel.EncoderChoices
        };

        BindingOperations.SetBinding(
            comboBox,
            ComboBox.SelectedValueProperty,
            new Binding(nameof(SettingsViewModel.VideoEncoder))
            {
                Source = viewModel,
                Mode = BindingMode.TwoWay
            });

        comboBox.ApplyTemplate();

        return comboBox;
    }

    /// <summary>
    /// Список пресетов выгрузки из разметки: выбор приходит по самому пункту, а не по значению
    /// внутри него, поэтому у списка нет <c>SelectedValuePath</c>.
    /// </summary>
    private static ComboBox BindPresets(SettingsViewModel viewModel)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = viewModel.ExportPresets
        };

        BindingOperations.SetBinding(
            comboBox,
            ComboBox.SelectedItemProperty,
            new Binding(nameof(SettingsViewModel.SelectedExportPreset))
            {
                Source = viewModel,
                Mode = BindingMode.TwoWay
            });

        comboBox.ApplyTemplate();

        return comboBox;
    }
}