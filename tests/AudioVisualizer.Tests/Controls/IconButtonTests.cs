using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Рисунки кнопок-иконок, шаблон кнопки и стиль рисунка.
/// </summary>
/// <remarks>
/// Тестовый хост не запускает приложение, поэтому ресурсы подгружаются вручную:
/// <see cref="AppResources"/> собирает из настоящего App.xaml ту же картину, что и
/// приложение. Все стили и шаблоны берутся из исходного файла, а не копируются в тест:
/// проверка должна видеть ровно то, что увидит приложение.
/// </remarks>
[Collection(WpfUi.Name)]
public partial class IconButtonTests
{
    private static readonly string[] IconKeys =
    [
        "IconOpen", "IconSave", "IconSaveAs", "IconDelete", "IconReset",
        "IconPlay", "IconPause", "IconStop", "IconExport", "IconCancel"
    ];

    private static readonly Lazy<ResourceDictionary> AppStyles = new(() =>
    {
        var xaml = File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "src", "AudioVisualizer.App", "App.xaml"));

        var wrapped =
            "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
            BrushDeclarations() +
            Extract(xaml, "<ControlTemplate x:Key=\"IconButtonTemplate\"", "</ControlTemplate>") +
            Extract(xaml, "<Style x:Key=\"IconButton\"", "</Style>") +
            Extract(xaml, "<Style x:Key=\"IconPath\"", "</Style>") +
            "</ResourceDictionary>";

        return (ResourceDictionary)XamlReader.Parse(wrapped);
    });

    [Fact]
    public void Иконки_СуществуютИНепустые()
    {
        Sta.Run(() =>
        {
            foreach (var key in IconKeys)
            {
                Assert.False(Icon(key).IsEmpty(), $"Иконка {key} пустая.");
            }
        });
    }

[Fact]
    public void Иконки_ЗаполняютСвоюРамку()
    {
        Sta.Run(() =>
        {
            // Path вписывает рисунок в квадрат 16x16 по большей стороне, поэтому размер на
            // экране задаёт отношение сторон габаритов, а не координаты: знак в сетке 54x54
            // и знак в сетке 24x24 рисуются одного размера. Проверяется поэтому доля от
            // меньшей стороны к большей - то, что действительно видно.
            //
            // Порог ловит сплющенные и уехавшие в угол знаки: у «паузы» из двух тонких
            // полосок было 0.62, и она читалась мельче корзины из такой же рамки.
            const double MinimumFill = 0.7d;

            foreach (var key in IconKeys)
            {
                var bounds = Icon(key).Bounds;

                Assert.True(bounds.Width > 0 && bounds.Height > 0, $"Иконка {key} вырожденная: {bounds}.");

                var fill = Math.Min(bounds.Width, bounds.Height) / Math.Max(bounds.Width, bounds.Height);

                Assert.True(fill >= MinimumFill, $"Иконка {key} заполняет рамку лишь на {fill:0.00}: {bounds}.");
            }
        });
    }

    [Fact]
    public void ИконкиРазличаютсяМеждуСобой()
    {
        Sta.Run(() =>
        {
            // Случайно повторившийся контур тихо превратил бы две разные кнопки в одну.
            // Сравнивается содержимое фигур, а не ToString: у группы геометрий он одинаковый
            // у всех иконок и ничего не различает.
            var unique = IconKeys.Select(key => Describe(Icon(key))).Distinct().Count();

            Assert.Equal(IconKeys.Length, unique);
        });
    }

    [Fact]
    public void ГеометрияДоезжаетДоРисункаВКнопке()
    {
        Sta.Run(() =>
        {
            var icon = Icon("IconDelete");

            var button = CreateButton(icon);

            var path = FindPath(button);

            Assert.NotNull(path);
            Assert.Same(icon, path!.Data);
        });
    }

    [Fact]
    public void ОбводкаБерётсяИзПалитры()
    {
        Sta.Run(() =>
        {
            // Раньше обводка шла от Foreground кнопки, и состояний у иконки не было вовсе.
            // Теперь цвет приходит из словаря и меняется сам при наведении и отключении.
            var button = CreateButton(Icon("IconStop"));

            AssertBrush("IconBrush", FindPath(button)!.Stroke);
        });
    }

    [Fact]
    public void ОтключённаяКнопкаГаситсяЦеликом()
    {
        Sta.Run(() =>
        {
            // Отдельного цвета у отключённой иконки нет: гасит вся кнопка в шаблоне. Раньше
            // это проверялось поведением триггера на IsEnabled рисунка, но он и не
            // срабатывал: {Binding IsEnabled} без источника идёт в DataContext, а не в
            // свойство элемента.
            var template = (ControlTemplate)AppStyles.Value["IconButtonTemplate"];
            var disabled = template.Triggers
                .OfType<Trigger>()
                .FirstOrDefault(trigger => trigger.Property == UIElement.IsEnabledProperty);

            Assert.NotNull(disabled);
            Assert.NotEmpty(disabled!.Setters);
        });
    }

    [Fact]
    public void УРисункаОдноСостояние_Наведение()
    {
        Sta.Run(() =>
        {
            // Наведение приходится спрашивать у кнопки-предка: IsMouseOver не наследуется.
            // Второго состояния нет, и добавлять его через IsEnabled предка не стоит - такой
            // триггер держится на визуальном дереве и слетает до измерения кнопки.
            var triggers = IconPathStyle().Triggers;

            Assert.Single(triggers);

            var trigger = Assert.IsType<DataTrigger>(triggers[0]);
            var binding = Assert.IsType<System.Windows.Data.Binding>(trigger.Binding);

            Assert.Equal(System.Windows.Data.RelativeSourceMode.FindAncestor, binding.RelativeSource.Mode);
            Assert.Equal(typeof(Button), binding.RelativeSource.AncestorType);
        });
    }

    /// <summary>
    /// Кисти сравниваются по цвету, а не по ссылке: стиль в тесте разобран из файла и
    /// содержит собственный экземпляр кисти, а не тот же объект, что в словаре приложения.
    /// </summary>
    private static void AssertBrush(string expectedKey, Brush actual)
    {
        var expected = (SolidColorBrush)Brush(expectedKey);

        Assert.Equal(expected.Color, Assert.IsType<SolidColorBrush>(actual).Color);
    }

    /// <summary>
    /// Кнопка с рисунком, помещённая в контейнер и размеченная: состояния рисунка и
    /// содержимое шаблона появляются только после измерения.
    /// </summary>
    private static Button CreateButton(Geometry icon, bool enabled = true)
    {
        var button = new Button
        {
            Style = ButtonStyle(),
            Content = new Path { Style = IconPathStyle(), Data = icon },
            IsEnabled = enabled
        };

        var host = new Grid();
        host.Children.Add(button);
        host.Measure(new Size(64d, 64d));
        host.Arrange(new Rect(0d, 0d, 64d, 64d));

        return button;
    }

    [Fact]
    public void СостоянияИконкиОтличаютсяОтОбычногоЦвета()
    {
        Sta.Run(() =>
        {
            // Три одинаковые кисти означали бы, что состояний нет вовсе.
            var brushes = new[] { Brush("IconBrush"), Brush("IconHoverBrush") };

            Assert.Equal(brushes.Length, brushes.Distinct().Count());
        });
    }

    [Fact]
    public void ФокусУКнопкиИконкиВыключен()
    {
        Sta.Run(() =>
        {
            // У кнопки без подписи рамка фокуса только мешает.
            var button = new Button { Style = ButtonStyle() };

            Assert.False(button.Focusable);
        });
    }

    [Fact]
    public void СтильИШаблон_ЛежатВСловареПриложения()
    {
        // Словарь с рисунками подключается через Source и грузится лениво: значение, которое
        // тянет ресурс из словаря приложения, при обращении к ключу создаёт само себя и
        // уводит WPF в переполнение стека на запуске.
        var icons = StripComments(File.ReadAllText(System.IO.Path.Combine(
            RepositoryRoot(), "src", "AudioVisualizer.App", "Theme", "Icons.xaml")));
        var app = File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "src", "AudioVisualizer.App", "App.xaml"));

        foreach (var key in new[] { "IconButton", "IconPath", "IconButtonTemplate" })
        {
            Assert.DoesNotContain($"x:Key=\"{key}\"", icons, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"{key}\"", app, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void СловарьКонтуров_НеСсылаетсяНаРесурсы()
    {
        var markup = StripComments(File.ReadAllText(System.IO.Path.Combine(
            RepositoryRoot(), "src", "AudioVisualizer.App", "Theme", "Icons.xaml")));

        Assert.DoesNotContain("DynamicResource", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticResource", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("TemplateBinding", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ВСловаряхПриложения_НетПодстановкиРесурса()
    {
        // Подстановка по смене ресурса в словарях приложения пока не проверена запуском:
        // найденная поломка была не в ней, но и безопасность её не доказана. Проверка
        // закрывает само решение, а не его следствие.
        foreach (var relative in new[] { "App.xaml", "MainWindow.xaml", System.IO.Path.Combine("Controls", "ColorPicker.xaml") })
        {
            var markup = StripComments(File.ReadAllText(System.IO.Path.Combine(
                RepositoryRoot(), "src", "AudioVisualizer.App", relative)));

            Assert.DoesNotContain("DynamicResource", markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ВКнопкахОсталисьПодписи()
    {
        // Подпись переехала в ToolTip и в имя для программы чтения с экрана. Без первого
        // кнопка непонятна, без второго - безымянна. Проверяется каждая кнопка целиком: у
        // кнопки переключения подсказок два - в сеттере и в его триггере.
        var markup = File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "src", "AudioVisualizer.App", "MainWindow.xaml"));
        var buttons = ButtonBlockPattern().Matches(markup).Cast<Match>().ToArray();

        Assert.NotEmpty(buttons);
        Assert.All(buttons, button =>
        {
            Assert.True(
                Names(button.Value, "ToolTip"),
                "Кнопка без подсказки: " + Compact(button.Value));
            Assert.True(
                Names(button.Value, "AutomationProperties.Name"),
                "Кнопка без имени для чтения с экрана: " + Compact(button.Value));
        });
    }

    /// <summary>
    /// Имя задано либо прямо на кнопке, либо сеттером её стиля: у кнопки переключения
    /// подсказки живут в стиле и в его триггере, потому что локальные значения перебили бы
    /// триггер.
    /// </summary>
    private static bool Names(string block, string name)
        => block.Contains($"{name}=\"", StringComparison.Ordinal)
            || block.Contains($"Property=\"{name}\"", StringComparison.Ordinal);

    private static string Compact(string xaml)
        => string.Join(' ', xaml.Split('\n').Select(line => line.Trim())).Replace("  ", " ")[..Math.Min(160, string.Join(' ', xaml.Split('\n').Select(line => line.Trim())).Length)];

    [Fact]
    public void ВПодсказкахНетПояснений()
    {
        // Подсказка называет действие, а не объясняет его: «Сбросить цвета: вернуть значения
        // по умолчанию только для этой вкладки» - это текст на две строки там, где хватает
        // одного. Объяснение живёт в комментарии над кнопкой.
        var markup = File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "src", "AudioVisualizer.App", "MainWindow.xaml"));
        var tooltips = ToolTipPattern().Matches(markup).Cast<Match>().Select(match => match.Groups["text"].Value).ToArray();

        Assert.NotEmpty(tooltips);
        Assert.All(tooltips, tooltip => Assert.DoesNotContain(":", tooltip, StringComparison.Ordinal));
    }

    /// <summary>Комментарии вырезаются: словари объясняют в комментарии, почему ссылок быть не должно.</summary>
    private static string StripComments(string xaml) => CommentPattern().Replace(xaml, string.Empty);

    private static string Describe(Geometry geometry)
        => string.Join(
            "|",
            geometry.GetFlattenedPathGeometry().Figures
                .Select(figure => figure.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Готовит ресурсы приложения один раз на процесс.</summary>
    private static void EnsureResources() => AppResources.Ensure();

    private static object? Resource(string key)
    {
        EnsureResources();

        return Application.Current.TryFindResource(key);
    }

    private static Geometry Icon(string key)
        => Resource(key) as Geometry ?? throw new InvalidOperationException($"Иконка \"{key}\" не найдена или это не геометрия.");

    private static Brush Brush(string key)
        => Resource(key) as Brush ?? throw new InvalidOperationException($"Кисть \"{key}\" не найдена.");

    private static Style ButtonStyle() => (Style)AppStyles.Value["IconButton"];

    private static Style IconPathStyle() => (Style)AppStyles.Value["IconPath"];

    /// <summary>Объявления кистей из App.xaml: стиль рисунка ссылается на них при разборе.</summary>
    private static string BrushDeclarations()
    {
        var xaml = File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "src", "AudioVisualizer.App", "App.xaml"));

        return string.Join(
            string.Empty,
            AppResources.BrushPattern().Matches(xaml).Select(match => match.Value));
    }

    private static string Extract(string xaml, string startMarker, string endTag)
    {
        var start = xaml.IndexOf(startMarker, StringComparison.Ordinal);

        if (start < 0)
        {
            throw new InvalidOperationException($"В App.xaml не найдено \"{startMarker}\".");
        }

        var end = xaml.IndexOf(endTag, start, StringComparison.Ordinal);

        if (end < 0)
        {
            throw new InvalidOperationException($"В App.xaml не закрыт \"{startMarker}\".");
        }

        return xaml[start..(end + endTag.Length)];
    }

    private static Path? FindPath(DependencyObject root)
    {
        if (root is Path path)
        {
            return path;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0; index < count; index++)
        {
            var found = FindPath(VisualTreeHelper.GetChild(root, index));

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "AudioVisualizer.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Корень репозитория не найден рядом с папкой сборки тестов.");
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    /// <summary>Кнопка целиком: либо закрытая одинарным тегом, либо вместе со своим содержимым.</summary>
    [GeneratedRegex("""<Button(?=[\s>])[^>]*/>|<Button(?=[\s>])[^>]*>.*?</Button>""", RegexOptions.Singleline)]
    private static partial Regex ButtonBlockPattern();

    [GeneratedRegex("(?:ToolTip|Property=\"ToolTip\" Value)=\"(?<text>[^\"]+)\"")]
    private static partial Regex ToolTipPattern();
}