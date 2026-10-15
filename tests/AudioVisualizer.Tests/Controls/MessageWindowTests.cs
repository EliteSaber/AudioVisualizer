using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AudioVisualizer.App;
using AudioVisualizer.App.ViewModels;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Окно сообщения: заголовок, прокручиваемый текст и кнопка папки. Проверяется на STA-потоке,
/// как и остальные элементы интерфейса.
/// </summary>
/// <remarks>
/// Окно не показывается - проверяются только заголовок, текст и видимость кнопки. Открывающее
/// действие подставляется, поэтому проводник в тесте не запускается: ради одного утверждения
/// поднимать приложение было бы лишним. Это то же решение, что с <c>SpectrumElement.OnRender</c>,
/// только без наследования - WPF ищет разметку в сборке runtime-типа, и подкласс окна в тестах
/// её не находит.
/// </remarks>
[Collection(WpfUi.Name)]
public class MessageWindowTests
{
    [Fact]
    public void ЗаголовокИТекст_ПопадаютНаСвоиМеста() => Sta.Run(() =>
    {
        var window = new MessageWindow(new Notice("Не удалось выгрузить видео", "ffmpeg перестал читать кадры"));

        Assert.Equal("Не удалось выгрузить видео", window.HeadingBlock.Text);
        Assert.Equal("ffmpeg перестал читать кадры", window.MessageBox.Text);
        Assert.Equal("Не удалось выгрузить видео", window.Title);
    });

    [Fact]
    public void Текст_ТолькоДляЧтенияИПрокручивается() => Sta.Run(() =>
    {
        var window = new MessageWindow(new Notice("Ошибка", new string('а', 4000)));

        // Поле должно быть прокручиваемым и ограниченным по высоте: иначе окно выросло бы
        // на весь текст и перестало помещаться на экране.
        Assert.True(window.MessageBox.IsReadOnly);
        Assert.Equal(TextWrapping.Wrap, window.MessageBox.TextWrapping);
        Assert.Equal(ScrollBarVisibility.Auto, window.MessageBox.VerticalScrollBarVisibility);
        Assert.InRange(window.MessageBox.MaxHeight, 1d, double.MaxValue);
        Assert.True(double.IsFinite(window.MaxHeight));
    });

    [Fact]
    public void БезПапки_КнопкаСкрыта() => Sta.Run(() =>
    {
        var window = new MessageWindow(new Notice("Ошибка", "текст"));

        // У ошибки папки нет вовсе: ролик не появился, журнала на диске не осталось.
        Assert.Equal(Visibility.Collapsed, window.FolderButton.Visibility);
    });

    [Fact]
    public void ПапкиНетНаДиске_КнопкаСкрыта() => Sta.Run(() =>
    {
        var window = new MessageWindow(new Notice("Выгрузка готова", "текст", @"C:\нет\такого"));

        // Путь есть, а папки нет: кнопка была бы обещанием, которое не выполняется.
        Assert.Equal(Visibility.Collapsed, window.FolderButton.Visibility);
    });

    [Fact]
    public void ПапкаЕсть_КнопкаОткрываетЕё() => Sta.Run(() =>
    {
        using var directory = new TemporaryDirectory();

        var opened = new List<string>();
        var window = new MessageWindow(new Notice("Выгрузка готова", "текст", directory.Root), opened.Add);

        Assert.Equal(Visibility.Visible, window.FolderButton.Visibility);

        window.FolderButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.Equal(directory.Root, Assert.Single(opened));
    });
}