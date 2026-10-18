using System.Xml.Linq;

namespace AudioVisualizer.Tests.Theme;

/// <summary>
/// Разметка окна читается как текст: поднять само окно в тестах нельзя, оно создаёт
/// настоящий файл настроек и рисует интерфейс. Так же поступает <see cref="ThemeResourceTests"/>.
/// </summary>
/// <remarks>
/// Проверяются не картинки, а признаки, которые ломаются молча: длинная подсказка без
/// переноса строки вылезает за панель, и это видно только глазами. Подсказка на элементе,
/// а не на его подписи, видна только при наведении на подпись.
/// </remarks>
public class SettingsMarkupTests
{
    /// <summary>Короче этой длины подсказка помещается в панель при любой её ширине.</summary>
    private const int LongHintLength = 40;

    [Fact]
    public void ДлиннаяПодсказка_Переносится()
    {
        var hints = LongHints().ToArray();

        // Проверка на пустом наборе прошла бы вхолостую: так и вышло бы, если бы фильтр
        // перестал видеть подсказки после правки разметки.
        Assert.True(hints.Length > 0, "Длинных подсказок не найдено - правило проверяет ничто.");

        var unwrapped = hints
            .Where(element => (string?)element.Attribute("TextWrapping") is null)
            .Select(element => (string?)element.Attribute("Text"))
            .ToArray();

        Assert.True(unwrapped.Length == 0, "Подсказки без переноса: " + string.Join(" | ", unwrapped));
    }

    [Fact]
    public void ПодсказкаСтрокиСтатуса_НеЗаданаРазметкой()
    {
        var status = Status();

        // Подсказку назначает StatusHint.Attach: содержимое ToolTip живёт в попапе, DataContext
        // в него не наследуется, и привязка внутри подсказки выглядит работающей, пока не
        // окажется пустой. Так и вышло - подсказка была пустым белым квадратом.
        Assert.DoesNotContain(status.Elements(), element => element.Name.LocalName.EndsWith(".ToolTip", StringComparison.Ordinal));

        // Имя нужно коду окна, которое зовёт хелпер. Ищется по имени, а не по пространству
        // имён: в разметке это x:Name.
        var name = status.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Name")?.Value;

        Assert.Equal("StatusText", name);
    }

    [Fact]
    public void ПустаяПодсказка_Выключена()
    {
        // Подробности есть не у каждого сообщения, а попап с пустым текстом всё равно
        // появляется - маленьким пустым прямоугольником под обычным сообщением.
        var triggers = Status().Descendants().Where(element => element.Name.LocalName == "DataTrigger").ToArray();

        Assert.Contains(triggers, trigger =>
            trigger.Attribute("Binding")?.Value == "{Binding StatusDetail}"
            && (string?)trigger.Attribute("Value") == string.Empty
            && trigger.Elements().Any(setter => (string?)setter.Attribute("Property") == "ToolTipService.IsEnabled"));
    }

    [Fact]
    public void ПодсказкаТихойВыгрузки_ЗаданаНаСтроке()
    {
        var row = Row("Тихая выгрузка");

        Assert.False(string.IsNullOrWhiteSpace((string?)row.Attribute("ToolTip")));

        // На самом чекбоксе подсказки быть не должно: иначе текст живёт в двух местах и
        // правки разъезжаются, а над подписью его не будет.
        var checkbox = row.Descendants().Single(element => element.Name.LocalName == "CheckBox");

        Assert.Null(checkbox.Attribute("ToolTip"));
    }

    [Fact]
    public void ПодсказкаКодировщиков_СкрытаКогдаГоворитьНечего()
    {
        // При найденной видеокарте сообщения нет вовсе, а блок с отступом всё равно держал бы
        // высоту: пустая строка под списком стоила пользователю пустого места.
        var message = MainWindow()
            .Descendants()
            .Single(element => (string?)element.Attribute("Text") == "{Binding Settings.EncodersMessage}");

        Assert.Contains("Visibility", (string?)message.Attribute("Visibility"));
        Assert.Contains("HasEncodersMessage", (string?)message.Attribute("Visibility"), StringComparison.Ordinal);
    }

    /// <summary>Надпись строки статуса: единственная в окне, у которой текст привязан к Status.</summary>
    private static XElement Status()
        => MainWindow()
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock" && (string?)element.Attribute("Text") == "{Binding Status}");

    private static XElement Row(string label)
        => MainWindow()
            .Descendants()
            .Single(element => element.Name.LocalName == "SettingRow" && (string?)element.Attribute("Label") == label);

    private static IEnumerable<XElement> LongHints()
        => MainWindow()
            .Descendants()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => (Text: (string?)element.Attribute("Text"), Element: element))
            // Привязанный текст не в счёт: его длина в разметке ничего не значит.
            .Where(pair => pair.Text is { Length: > LongHintLength } && !pair.Text.StartsWith("{Binding", StringComparison.Ordinal))
            .Select(pair => pair.Element);

    private static XDocument MainWindow()
        => XDocument.Parse(File.ReadAllText(Path.Combine(AppRoot(), "MainWindow.xaml")));

    private static string AppRoot()
    {
        // Путь до исходников ищется так же, как в ThemeResourceTests.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "AudioVisualizer.App");

            if (File.Exists(Path.Combine(candidate, "App.xaml")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не найден каталог src\\AudioVisualizer.App.");
    }
}