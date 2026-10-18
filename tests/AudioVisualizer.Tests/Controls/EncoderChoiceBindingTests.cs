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
}