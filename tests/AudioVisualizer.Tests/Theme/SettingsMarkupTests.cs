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
    public void ПодсказкаЭквалайзераВЗвуке_ЗаданаНаСтроке()
    {
        var row = Row("Эквалайзер в звуке");

        Assert.False(string.IsNullOrWhiteSpace((string?)row.Attribute("ToolTip")));

        // На самом чекбоксе подсказки быть не должно: иначе текст живёт в двух местах
        // и правки разъезжаются, а над подписью его не будет.
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

    [Fact]
    public void РаскрытыйСписок_НеПохожНаПоле()
    {
        // Список красил себя кистью поля, поэтому открытый и закрытый списки выглядели
        // одинаково и вправду были одного цвета. Перекраска попапа в этом не помогала: его
        // заливал плотный ряд пунктов, и смена PopupBackground меняла только попап палитры
        // цвета. Видно только глазами, поэтому обе причины проверяются по разметке.
        var surface = PopupSurface();

        // Проверка на "PopupBackground" отсекает возврат на палитру поля отдельной проверкой.
        Assert.Contains("PopupBackground", (string?)surface.Attribute("Background") ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("PopupBorderBrush", (string?)surface.Attribute("BorderBrush") ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ПунктСписка_НеЗаливаетСамСебя()
    {
        // Вторая половина той же поломки, и она незаметнее: пока пункт непрозрачен, попап под
        // ним не виден, и заливка попапа не значит ничего. Заливку списка задаёт рамка попапа.
        var itemRoot = AppXaml()
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "ItemRoot"));

        var background = (string?)itemRoot.Attribute("Background") ?? string.Empty;

        Assert.False(
            background.Contains("Resource", StringComparison.Ordinal),
            $"Пункт списка заливает себя сам ({background}) и закрывает попап под собой.");
    }

    [Fact]
    public void РаскрытыйСписок_ОтделёнТенью()
    {
        // Цветом список от панели не отделяется: PopupBackground и PanelBackground различаются
        // на три единицы из 255, то есть для глаза это один цвет, и остаётся только рамка.
        // Тень обязательна, и места под неё хватать тоже: эффект не входит в размер элемента,
        // и окно попапа обрежет её по своим границам, то есть тихо и незаметно.
        var popup = Popup();

        var surface = PopupSurface();

        Assert.Contains("Border.Effect", surface.Descendants().Select(element => element.Name.LocalName));

        var spacer = popup.Elements().Single();

        Assert.False(
            string.IsNullOrWhiteSpace((string?)spacer.Attribute("Margin")),
            "У попапа нет полей под тень: окно попапа обрежет её по своим границам.");
    }

    [Fact]
    public void БлокВыгрузки_НеПрокручивается()
    {
        // Блок выгрузки стоял последним в прокручиваемом списке и уезжал за верхний край
        // панели ровно тогда, когда он и нужен: когда человек ждёт этапа выгрузки, полосы
        // и кнопки отмены. Проверяется правило, а не картинка: вернуть блок внутрь
        // ScrollViewer можно случайно, и в сборке это ничем не проявится.
        var scroller = Scroller();

        foreach (var binding in ExportBindings)
        {
            Assert.False(
                Mentions(scroller, binding),
                $"Привязка {binding} вернулась внутрь прокрутки: блок выгрузки снова уезжает за верхний край панели.");

            Assert.True(
                Mentions(ExportTab(), binding),
                $"Привязка {binding} не найдена на вкладке выгрузки вовсе.");
        }
    }

    [Fact]
    public void БлокВыгрузки_СтоитПодПрокруткой()
    {
        // Прибитое место должно быть отдельной строкой под прокруткой. Ряд задан явно у
        // обоих: без ScrollViewer в нулевой строке он растянулся бы на обе строки, и
        // прибитый блок уехал бы под пределы вкладки вместе с настройками.
        var grid = ExportTab().Elements().Single();

        Assert.Equal("Grid", grid.Name.LocalName);

        var heights = grid.Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements()
            .Select(row => (string?)row.Attribute("Height") ?? string.Empty)
            .ToArray();

        Assert.Equal(["*", "Auto"], heights);

        Assert.Equal("0", (string?)Scroller().Attribute("Grid.Row"));
        Assert.Equal("1", (string?)Footer().Attribute("Grid.Row"));

        // Место под полосу зарезервировано постоянно, а сама полоса появляется и пропадает:
        // иначе прибитый блок вырос бы на её высоту в момент старта выгрузки, и кнопки
        // уехали бы вверх прямо под рукой.
        var progress = Footer().Descendants()
            .Single(element => element.Name.LocalName == "ProgressBar");

        var reserved = Assert.IsType<XElement>(progress.Parent);

        Assert.False(
            string.IsNullOrWhiteSpace((string?)reserved.Attribute("Height")),
            "Под полосу прогресса не зарезервировано место: блок выгрузки прыгает при старте выгрузки.");
    }

    [Fact]
    public void ПолзунокГромкости_ЗадаётЧастотуТиков()
    {
        var slider = VolumeSlider();

        // Глобальный стиль ползунков включает привязку к тикам всем, а у TickFrequency в WPF
        // умолчание 1.0: без явной частоты ползунок 0…1 щёлкает только краями - ноль или
        // сто процентов, что и сказал первый пользователь. Ползунок позиции от того же
        // грабли спасает только масштаб: у него тик - целая секунда, и уши его не замечают.
        Assert.Equal("0.01", (string?)slider.Attribute("TickFrequency"));
    }

    [Fact]
    public void ПолзунокГромкости_ЗадаётШагКлавиш()
    {
        var slider = VolumeSlider();

        // У RangeBase LargeChange по умолчанию 1, а у ползунка 0…1 это PageUp с нуля на
        // сто процентов. SmallChange в умолчании 0.1 - это десять процентов на стрелку,
        // для громкости слишком много.
        Assert.Equal("0.01", (string?)slider.Attribute("SmallChange"));
        Assert.Equal("0.1", (string?)slider.Attribute("LargeChange"));
    }

    [Fact]
    public void ПолзунокГромкости_ИмяПостоянное()
    {
        // Имя элемента для чтения с экрана не должно меняться вместе со значением: значение
        // ползунка и так объявляется само, а имя с процентом внутри только шумит.
        Assert.Equal("Громкость", (string?)VolumeSlider().Attribute("AutomationProperties.Name"));
    }

    /// <summary>Ползунок громкости: ищется по привязке, а не по имени.</summary>
    private static XElement VolumeSlider()
        => MainWindow().Descendants().Single(element => element.Name.LocalName == "Slider"
            && (string?)element.Attribute("Value") == "{Binding Volume}");

    /// <summary>
    /// Привязки блока выгрузки. Ищутся в любом атрибуте: по <c>Command</c>, <c>Value</c> и
    /// <c>Text</c> по отдельности проверка разъехалась бы с разметкой при любой перестановке.
    /// </summary>
    private static readonly string[] ExportBindings =
    [
        "ExportVideoCommand",
        "CancelExportCommand",
        "ExportProgress",
        "ExportProgressText"
    ];

    /// <summary>Вкладка выгрузки: ищется по <c>x:Name</c>, а не по заголовку.</summary>
    private static XElement ExportTab()
        => MainWindow().Descendants().Single(Named("ExportTab"));

    /// <summary>Прибитый блок выгрузки: то же имя, только подпись границы.</summary>
    private static XElement Footer()
        => ExportTab().Descendants().Single(Named("ExportFooter"));

    /// <summary>Прокручиваемая половина вкладки. Она единственная, вложенных нет.</summary>
    private static XElement Scroller()
        => ExportTab().Descendants().Single(element => element.Name.LocalName == "ScrollViewer");

    /// <summary>Имя ищется по <c>Name.LocalName</c>: в разметке это <c>x:Name</c>.</summary>
    private static Func<XElement, bool> Named(string name)
        => element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);

    private static bool Mentions(XElement scope, string binding)
        => scope.Descendants()
            .Any(element => element.Attributes().Any(attribute => attribute.Value.Contains(binding, StringComparison.Ordinal)));

    /// <summary>
    /// Поверхность попапа списка: та, что залита кистью из словаря. Рамок подряд две, и обе
    /// прозрачные, так что признак один - ссылка на ресурс.
    /// </summary>
    private static XElement PopupSurface()
    {
        var popup = Popup();

        return popup.Descendants().Single(element =>
            element.Name.LocalName == "Border" && Painted(element));
    }

    private static bool Painted(XElement border)
        => ((string?)border.Attribute("Background"))?.Contains("StaticResource", StringComparison.Ordinal) == true;

    private static XElement Popup()
    {
        var popup = AppXaml()
            .Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "Popup"
                // Имя ищется по x:Name, как и в разметке окна.
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "PART_Popup"));

        Assert.NotNull(popup);

        return popup!;
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

    /// <summary>Словарь приложения: попап списка объявлен там же, где и кисти.</summary>
    private static XDocument AppXaml()
        => XDocument.Parse(File.ReadAllText(Path.Combine(AppRoot(), "App.xaml")));

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