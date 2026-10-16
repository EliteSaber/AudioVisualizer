using System.Text.RegularExpressions;

namespace AudioVisualizer.Tests.Theme;

/// <summary>
/// Проверки словаря ресурсов приложения. Обе ловят поломку, которая иначе остаётся
/// незамеченной до запуска окна.
/// </summary>
/// <remarks>
/// Кисти и словари проверяются чтением файла, а не разбором разметки: поломка такого рода
/// не видна в сборке. Пример — объявление кисти, ссылающееся на сам себя
/// (<c>Color="{StaticResource TabHoverBrush}"</c> в объявлении <c>TabHoverBrush</c>):
/// с подстановкой по смене ресурса она молча даёт пустой цвет, с обычной ссылкой -
/// падение при разборе с сообщением о ненайденном ресурсе, хотя виновник рядом.
/// </remarks>
public partial class ThemeResourceTests
{
    [Fact]
    public void УКаждойКисти_ЦветЗаданЛитералом()
    {
        foreach (Match declaration in BrushPattern().Matches(AppXaml()))
        {
            var key = declaration.Groups["key"].Value;
            var color = declaration.Groups["color"].Value;

            Assert.True(color.StartsWith('#'), $"Кисть {key} ссылается на ресурс вместо цвета: Color=\"{color}\".");
        }
    }

    [Fact]
    public void КлючиРесурсов_НеПовторяются()
    {
        // Дубль ключа даёт ту же ошибку «не удаётся найти ресурс», и указание на причину
        // указывает на место использования, а не на объявление.
        var duplicates = KeyPattern()
            .Matches(AppXaml())
            .Select(match => match.Groups["key"].Value)
            .GroupBy(key => key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.True(duplicates.Length == 0, "Ключи ресурсов повторяются: " + string.Join(", ", duplicates));
    }

[Fact]
    public void ОшибкаВСтатусе_Покрашена()
    {
        var window = MainWindowMarkup();

        // Кисть объявлена, а строка статуса переключается на неё по признаку модели: иначе
        // ошибка выглядела бы так же, как обычное сообщение, и её было бы не видать.
        Assert.Contains("StatusErrorBrush", window, StringComparison.Ordinal);
        Assert.Contains("IsStatusError", window, StringComparison.Ordinal);
        Assert.Contains("DataTrigger", window, StringComparison.Ordinal);
    }

    [Fact]
    public void КистьОшибкиОбъявлена()
    {
        // Отдельная проверка вместо общей: кисть в App.xaml есть всегда, а вот применять её
        // должна строка статуса - и без DataTrigger общая проверка молча бы прошла.
        Assert.Contains("x:Key=\"StatusErrorBrush\"", AppXaml(), StringComparison.Ordinal);
    }

    private static string MainWindowMarkup()
        => File.ReadAllText(Path.Combine(AppRoot(), "MainWindow.xaml"));

    [Fact]
    public void КистиОбъявлены_КаждаяИспользуется()
    {
        // Объявления читаются из App.xaml, а использование ищется по всей разметке: кисть,
        // объявленная один раз и применённая в другом файле, - самый обычный случай, а не
        // забытая краска. Искать применение только внутри App.xaml означало бы ругаться на
        // StatusErrorBrush, которая нужна окну, а не самому словарю.
        var declarations = AppXaml();
        var usage = string.Join(Environment.NewLine, MarkupFiles().Select(File.ReadAllText));

        var declared = BrushPattern()
            .Matches(declarations)
            .Select(match => match.Groups["key"].Value)
            .ToArray();

        Assert.NotEmpty(declared);
        Assert.All(declared, key => Assert.Contains(key, usage, StringComparison.Ordinal));
    }

    [Fact]
    public void КаждаяСсылкаНаРесурс_ИмеетОбъявление()
    {
        // Самая дорогая поломка запуска: ссылка есть, объявления нет. Приложение падает при
        // разборе разметки, и указание указывает на место использования, а не на причину.
        var declared = DeclaredKeys();

        var missing = ReferencedKeys()
            .Where(key => !declared.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0, "Ссылки без объявления: " + string.Join(", ", missing));
    }

    private static HashSet<string> DeclaredKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in MarkupFiles())
        {
            foreach (Match match in KeyPattern().Matches(File.ReadAllText(file)))
            {
                keys.Add(match.Groups["key"].Value);
            }
        }

        return keys;
    }

    private static string[] ReferencedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in MarkupFiles())
        {
            foreach (Match match in ReferencePattern().Matches(File.ReadAllText(file)))
            {
                keys.Add(match.Groups["key"].Value);
            }
        }

        return [.. keys];
    }

    private static List<string> MarkupFiles()
    {
        var root = AppRoot();
        var files = new List<string>
        {
            System.IO.Path.Combine(root, "App.xaml"),
            System.IO.Path.Combine(root, "MainWindow.xaml"),
            System.IO.Path.Combine(root, "MessageWindow.xaml"),
            System.IO.Path.Combine(root, "Theme", "Icons.xaml"),
            System.IO.Path.Combine(root, "Controls", "ColorPicker.xaml")
        };

        Assert.All(files, file => Assert.True(File.Exists(file), $"Не найден файл разметки {file}."));

        return files;
    }

    private static string AppXaml() => File.ReadAllText(System.IO.Path.Combine(AppRoot(), "App.xaml"));

    private static string AppRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "src", "AudioVisualizer.App");

            if (File.Exists(System.IO.Path.Combine(candidate, "App.xaml")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Каталог приложения не найден рядом с папкой сборки тестов.");
    }

    [GeneratedRegex("""<SolidColorBrush\s+x:Key="(?<key>[^"]+)"\s+Color="(?<color>[^"]*)"\s*/>""")]
    private static partial Regex BrushPattern();

    [GeneratedRegex("x:Key=\"(?<key>[^\"]+)\"")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("""\{(?:Static|Dynamic)Resource\s+(?<key>[^}\s]+)""")]
    private static partial Regex ReferencePattern();
}