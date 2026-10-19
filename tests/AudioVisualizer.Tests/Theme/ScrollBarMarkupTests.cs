using System.Xml.Linq;

namespace AudioVisualizer.Tests.Theme;

/// <summary>
/// Разметка полосы прокрутки читается как текст, тем же приёмом, что и остальная тема.
/// </summary>
/// <remarks>
/// Полосы стоят на четырёх вкладках, в раскрытом списке и в поле сообщения, и рисует их
/// система, пока у полосы нет своего шаблона. Что при этом видно, проверяется только глазами,
/// а что сломано - в сборке не видно вовсе: полоса без <c>PART_Track</c> и <c>PART_Thumb</c>
/// не падает и не ругается, она просто ничего не рисует.
///
/// Проверяется ещё и то, что цветов в разметке полосы нет: литерал здесь равен цвету,
/// зашитому в разметку мимо палитры, и светлая тема его не тронет.
/// </remarks>
public class ScrollBarMarkupTests
{
    [Fact]
    public void ПолосаЗаданаНеявнымСтилем()
    {
        // Неявным, а не через {StaticResource ScrollBarStyle}: полосы заводит не разметка,
        // а шаблоны ScrollViewer и TextBox, и на неявный стиль приложения они смотрят сами.
        var style = ScrollBarStyles().SingleOrDefault();

        Assert.NotNull(style);
        Assert.Contains("Template", SetterMarkup(style!), StringComparison.Ordinal);
    }

    [Fact]
    public void УПолосыЕстьОбязательныеЧасти()
    {
        // Без имени PART_Track ScrollBar.OnApplyTemplate не найдёт дорожку и тихо останется
        // без полосы. Ни одной ошибки при этом не возникает. Бегунок обязан лежать в
        // Track.Thumb: тип у этого места один, и вложенная дорожка туда не годится.
        var markup = ScrollBarTemplate();

        Assert.Contains("PART_Track", markup, StringComparison.Ordinal);
        Assert.Contains("Track.Thumb", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void КликПоДорожкеЛистаетСтраницей()
    {
        // Track обрабатывает мышь только за бегунком, а страницу листает нажатие на кнопке
        // под ним. Без кнопок клик по дорожке не делает ничего, и колесо остаётся единственным
        // способом прокрутки - на ноутбуке без колеса прокрутка исчезает целиком.
        foreach (var command in PageCommands)
        {
            Assert.Contains(command, ScrollBarTemplate(), StringComparison.Ordinal);
        }
    }

    /// <summary>Команды листания страницей: обе стороны дорожки.</summary>
    private static readonly string[] PageCommands = ["ScrollBar.PageUpCommand", "ScrollBar.PageDownCommand"];

    /// <summary>Ориентации полосы: обе должны быть заданы явно, а не одной по умолчанию.</summary>
    private static readonly string[] Orientations = ["Vertical", "Horizontal"];

    [Theory]
    [InlineData("Vertical")]
    [InlineData("Horizontal")]
    public void ОриентацияДорожкиЗаданаЯвно(string orientation)
    {
        // У Track ориентация по умолчанию горизонтальная, и полоса, получившая такую
        // дорожку, либо не видна, либо листает не туда. Проверяется по обоим триггерам:
        // задавать её только в одном означает оставить вторую ориентацию со значением по
        // умолчанию. Заодно проверяется разворот направления - без него дорожка идёт не в
        // ту сторону и значение полосы читалось бы наоборот.
        var setters = OrientationTrigger(orientation).Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Select(element => ((string?)element.Attribute("TargetName"), (string?)element.Attribute("Property")))
            .ToArray();

        Assert.Contains(("PART_Track", "Orientation"), setters);
        Assert.Contains(("PART_Track", "IsDirectionReversed"), setters);
    }

    [Fact]
    public void ПоляБегункаЗаданыПоОриентации()
    {
        // Отступ нужен поперёк полосы, чтобы отодвинуть бегунок от краёв дорожки, и не нужен
        // вдоль неё. Общими полями горизонтальный бегунок терял шесть пикселей своей длины,
        // и на глаз это выглядело как «полоса чуть короче», а не как ошибка полей.
        var margins = Orientations
            .Select(orientation => (string?)OrientationTrigger(orientation)
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Setter"
                    && (string?)element.Attribute("TargetName") == "PART_Thumb"
                    && (string?)element.Attribute("Property") == "Margin")
                .Attribute("Value"))
            .ToArray();

        Assert.All(margins, margin => Assert.False(string.IsNullOrWhiteSpace(margin)));
        Assert.NotEqual(margins[0], margins[1]);
    }

    private static XElement OrientationTrigger(string orientation)
    {
        var trigger = Template("ScrollBarTemplate").Descendants()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Trigger"
                && (string?)element.Attribute("Property") == "Orientation"
                && (string?)element.Attribute("Value") == orientation);

        Assert.NotNull(trigger);

        return trigger!;
    }

    [Fact]
    public void ВРазметкеПолосыНетЦветов()
    {
        // Цвет в разметке мимо палитры - это цвет, который светлая тема не тронет, и полоса
        // останется тёмной посреди светлой панели. Все её цвета обязаны быть в словаре.
        var literals = Templates()
            .Descendants()
            .Select(element => (string?)element.Attribute("Color")
                ?? (string?)element.Attribute("Background")
                ?? (string?)element.Attribute("Fill")
                ?? string.Empty)
            .Where(value => value.Contains('#'))
            .ToArray();

        Assert.True(literals.Length == 0, "В разметке полосы зашиты цвета: " + string.Join(" | ", literals));
    }

    [Fact]
    public void УБегункаСвойШаблон()
    {
        // Thumb - отдельный элемент со своим шаблоном, и без него система рисует бегунок сама.
        // Стилизация полосы при этом выглядит сделанной, а на экране виден системный бегунок.
        var thumb = Templates()
            .Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "Thumb");

        Assert.NotNull(thumb);
        Assert.Contains("ScrollBarThumb", (string?)thumb!.Attribute("Style") ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Разметка шаблона полосы: дорожка и бегунок объявлены прямо в нём, отдельного шаблона
    /// у дорожки нет - красить её нечем, Track себя не рисует.
    /// </summary>
    private static string ScrollBarTemplate() => Template("ScrollBarTemplate").ToString();

    /// <summary>Ключи шаблонов полосы: объявлены рядом с неявным стилем и нужны ему.</summary>
    private static readonly string[] TemplateKeys =
    [
        "ScrollBarTemplate", "ScrollBarThumbTemplate", "ScrollBarPageButtonTemplate"
    ];

    /// <summary>Шаблоны полосы. Отсутствие любого из них - поломка, а не повод проверить ничто.</summary>
    private static IEnumerable<XElement> Templates()
    {
        var document = AppXaml();
        var missing = TemplateKeys
            .Where(key => !document.Descendants().Any(element => KeyOf(element) == key))
            .ToArray();

        Assert.True(missing.Length == 0, "Не объявлены шаблоны полосы: " + string.Join(", ", missing));

        return TemplateKeys.Select(Template);
    }

    /// <summary>
    /// Шаблон по ключу. Ищется по всему словарю, а не внутри стиля: шаблоны объявлены рядом
    /// с ним, и вложенными в стиль они стали бы только случайно.
    /// </summary>
    private static XElement Template(string key)
    {
        var template = AppXaml().Descendants()
            .SingleOrDefault(element =>
                element.Name.LocalName == "ControlTemplate" && KeyOf(element) == key);

        Assert.NotNull(template);

        return template!;
    }

    /// <summary>
    /// Ключ объявления. Ищется по <c>Name.LocalName</c>, а не через <c>Attribute("Key")</c>:
    /// в разметке ключ объявлен как x:Key, и поиск по имени с префиксом находит его только
    /// так — по имени без пространства имён он не находит ничего и молча возвращает пустое.
    /// </summary>
    private static string? KeyOf(XElement element)
        => element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "Key")
            ?.Value;

    private static IEnumerable<XElement> ScrollBarStyles()
        => AppXaml()
            .Descendants()
            .Where(element => element.Name.LocalName == "Style"
                // Цель ищется по имени типа, как и в разметке: {x:Type ScrollBar}.
                && (string?)element.Attribute("TargetType") == "{x:Type ScrollBar}");

    /// <summary>Сеттеры стиля, а не всё под ним: шаблоны объявлены рядом и к сеттерам не относятся.</summary>
    private static string SetterMarkup(XElement style)
        => string.Join(Environment.NewLine, style.Elements()
            .Where(element => element.Name.LocalName == "Setter")
            .Select(element => element.ToString()));

    private static XDocument AppXaml()
        => XDocument.Parse(File.ReadAllText(Path.Combine(AppRoot(), "App.xaml")));

    private static string AppRoot()
    {
        // Путь до исходников ищется так же, как в ThemeResourceTests.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "AudioVisualizer.App");

            if (File.Exists(Path.Combine(candidate, "App.xaml")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Не найден каталог src\\AudioVisualizer.App.");
    }
}