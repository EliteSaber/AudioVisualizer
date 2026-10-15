using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;

namespace AudioVisualizer.Tests;

/// <summary>
/// Ресурсы приложения для тестов: кисти берутся из настоящего <c>App.xaml</c>, а словарь
/// контуров подключается как в приложении.
/// </summary>
/// <remarks>
/// Разметка элементов разбирается во время загрузки и находит ресурсы только в словаре
/// приложения. Пока в <c>ColorPicker.xaml</c> цвета были зашиты прямо в разметку, тестам
/// ничего не требовалось; после выноса палитры в словарь <c>StaticResource</c> перестал
/// разрешаться, и элементы переставали создаваться вовсе.
///
/// Словарь дополняется, а не заменяется: приложение в тестах одно на процесс, и подмена
/// целиком уронила бы любой тест, идущий после.
/// </remarks>
internal static partial class AppResources
{
    private static readonly Lazy<ResourceDictionary> Installed = new(Install);

    [GeneratedRegex("""<SolidColorBrush\s+x:Key="(?<key>[^"]+)"\s+Color="(?<color>#[0-9A-Fa-f]{6,8})"\s*/>""")]
    private static partial Regex BrushRegex();

    /// <summary>Готовит ресурсы один раз на процесс. Повторные вызовы ничего не делают.</summary>
    public static void Ensure() => _ = Installed.Value;

    /// <summary>Объявления кистей из App.xaml в виде разметки: тестам нужен такой же вид.</summary>
    public static Regex BrushPattern() => BrushRegex();

    private static ResourceDictionary Install()
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var resources = application.Resources;

        foreach (var pair in Brushes())
        {
            if (!resources.Contains(pair.Key))
            {
                resources[pair.Key] = pair.Value;
            }
        }

        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/AudioVisualizer.App;component/Theme/Icons.xaml")
        });

        return resources;
    }

    private static Dictionary<string, object> Brushes()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "AudioVisualizer.App", "App.xaml"));

        var declarations = BrushRegex()
            .Matches(xaml)
            .Select(match => $"<SolidColorBrush x:Key=\"{match.Groups["key"].Value}\" Color=\"{match.Groups["color"].Value}\" />")
            .ToArray();

        if (declarations.Length == 0)
        {
            throw new InvalidOperationException("В App.xaml не найдено ни одной кисти.");
        }

        var wrapped =
            "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
            string.Join(string.Empty, declarations) +
            "</ResourceDictionary>";

        var parsed = (ResourceDictionary)XamlReader.Parse(wrapped);
        var brushes = new Dictionary<string, object>();

        foreach (var key in parsed.Keys)
        {
            brushes[(string)key] = parsed[key];
        }

        return brushes;
    }

    /// <summary>Корень репозитория ищется по файлу решения: относительный путь от папки сборки меняется.</summary>
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AudioVisualizer.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Корень репозитория не найден рядом с папкой сборки тестов.");
    }
}