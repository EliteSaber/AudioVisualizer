using System.Windows;
using System.Windows.Controls;
using AudioVisualizer.App.Controls;
using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Подсказка строки статуса. Проверяется на STA-потоке, как остальные элементы интерфейса.
/// </summary>
/// <remarks>
/// Главная проверка здесь - разрешение привязки без всякого окна. Подсказка уже показывалась
/// пустым белым квадратом, и проверка разметки этого не поймала: она смотрела на форму
/// привязки, а не на её результат. С источником, указанным прямо, текст появляется сразу,
/// поэтому проверка обходится без показа окна.
/// </remarks>
[Collection(WpfUi.Name)]
public class StatusHintTests
{
    [Fact]
    public void ПодробностиСообщения_ПопадаютВПодсказку() => Sta.Run(() =>
    {
        var (viewModel, directory) = Create();
        using (directory)
        {
            var status = new TextBlock { DataContext = viewModel };

            StatusHint.Attach(status);
            viewModel.StatusDetail = "ffmpeg перестал читать кадры";

            // Привязка с готовым источником работает без окна и без попапа: если сюда попадёт
            // привязка, разрешаемая по дереву попаза, текст останется пустым.
            var hint = Assert.IsType<TextBlock>(status.ToolTip);

            Assert.Equal("ffmpeg перестал читать кадры", hint.Text);
        }
    });

    [Fact]
    public void ПовторноеНазначение_НеЛомаетПервуюПодсказку() => Sta.Run(() =>
    {
        var (viewModel, directory) = Create();
        using (directory)
        {
            // Привязка принадлежит месту, где задана, поэтому вторая подсказка обязана получить
            // свою: иначе первая молча перестала бы показывать текст.
            var first = new TextBlock { DataContext = viewModel };
            var second = new TextBlock { DataContext = viewModel };

            StatusHint.Attach(first);
            StatusHint.Attach(second);

            viewModel.StatusDetail = "причина";

            Assert.Equal("причина", Assert.IsType<TextBlock>(first.ToolTip).Text);
            Assert.Equal("причина", Assert.IsType<TextBlock>(second.ToolTip).Text);
        }
    });

    [Fact]
    public void ПодсказкаПоказываетПустоеСообщение() => Sta.Run(() =>
    {
        var (viewModel, directory) = Create();
        using (directory)
        {
            // Обычное сообщение подробностей не несёт, и подсказка показывает пустоту. Прятать
            // такую подсказку обязан триггер в разметке (ToolTipService.IsEnabled), а не хелпер:
            // свойства ToolTipService у него нет.
            var status = new TextBlock { DataContext = viewModel };

            StatusHint.Attach(status);

            Assert.Equal(string.Empty, Assert.IsType<TextBlock>(status.ToolTip).Text);
        }
    });

    [Fact]
    public void ДлинныйТекст_Переносится() => Sta.Run(() =>
    {
        var (viewModel, directory) = Create();
        using (directory)
        {
            var status = new TextBlock { DataContext = viewModel };

            StatusHint.Attach(status);

            // Попап измеряется без ограничения ширины: без переноса строка ffmpeg уехала бы за
            // край экрана, а без предела ширины перенос не начнётся.
            var hint = Assert.IsType<TextBlock>(status.ToolTip);

            Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
            Assert.InRange(hint.MaxWidth, 1d, double.MaxValue);
        }
    });

    [Fact]
    public void БезВладельца_Отказ() => Sta.Run(() =>
    {
        Assert.Throws<ArgumentNullException>(() => StatusHint.Attach(null!));
    });

    private static (MainViewModel ViewModel, TemporaryDirectory Directory) Create()
    {
        var directory = new TemporaryDirectory();

        return (new MainViewModel(new AppSettingsStore(directory.File("settings.json"))), directory);
    }
}