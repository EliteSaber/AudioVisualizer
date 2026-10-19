using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Шаблон полосы прокрутки проверяется настоящим элементом на STA-потоке: полоса без дорожки
/// и бегунка не падает и не ругается, она просто ничего не рисует, и в сборке это не видно.
/// </summary>
/// <remarks>
/// Разметка полосы вырезается из <c>App.xaml</c> целиком, а не пишется в проверке: вторая
/// копия шаблона разошлась бы с настоящей молча, и проверяла бы не то, что рисует
/// приложение. Объявления разбираются поверх кистей из настоящего словаря приложения —
/// иначе ссылки на цвета не разрешились бы, и разбор упал бы на первой же.
///
/// Элемент создаётся на общем STA-потоке, как и остальные проверки интерфейса.
/// </remarks>
[Collection(WpfUi.Name)]
public class ScrollBarTemplateTests
{
    /// <summary>Длина дорожки в разметке: вертикальной полосы по высоте, горизонтальной по ширине.</summary>
    private const double Length = 200d;

    /// <summary>Доля дорожки под бегунком при видимой части 100 и уводе на 20.</summary>
    private const double ThumbShare = 100d / 120d;

    [Fact]
    public void ЧастиПолосыНаходятсяПоСвоимИменам() => Sta.Run(() =>
    {
        var track = ДорожкаПолосы(Полоса());

        // Дорожка задаёт бегунку длину сама, поэтому без бегунка на полосе нечего рисовать.
        Assert.NotNull(track.Thumb);
        Assert.InRange(track.Thumb!.ActualHeight, 1d, Length);
    });

    [Fact]
    public void БегунокЗанимаетСвоюДолюДорожки() => Sta.Run(() =>
    {
        // Доля дорожки под бегунком задаётся видимой частью: из 120 единиц содержимого на
        // экране 100, поэтому бегунок занимает пять шестых. Растянутый на всю дорожку бегунок
        // не выглядит поломкой в коде, а на панели настроек прокрутка перестаёт читаться:
        // полоса есть, а двигаться нечему.
        //
        // Видимая часть и максимум заданы вручную: у полосы вне прокручиваемой области они
        // пустые, и дорожка делить высоту тогда не из чего.
        var track = ДорожкаПолосы(Полоса());

        AssertShare(track, ThumbShare, "height");
    });

    [Fact]
    public void ДорожкаВертикальнаяИРазвёрнута() => Sta.Run(() =>
    {
        // Ориентация у Track по умолчанию горизонтальная, а разворот без триггера не
        // делается: и то и другое проверяется только на живой полосе.
        var track = ДорожкаПолосы(Полоса());

        Assert.Equal(Orientation.Vertical, track.Orientation);
        Assert.True(track.IsDirectionReversed);
    });

    [Fact]
    public void ГоризонтальнаяПолосаНеКорочеСвоейШирины() => Sta.Run(() =>
    {
        // Горизонтальных полос в приложении нет, но шаблон один на обе ориентации. Проверяется
        // не рисунок, а то, что дорожка легла вдоль полосы, а не поперёк неё.
        var track = ДорожкаПолосы(Полоса(Orientation.Horizontal));

        Assert.Equal(Orientation.Horizontal, track.Orientation);
        Assert.False(track.IsDirectionReversed);
        AssertShare(track, ThumbShare, "width");
    });

    /// <summary>Доля дорожки, занятая бегунком. Числа в сообщении обязательны: без них
    /// «не так» не говорит, что именно сломалось.</summary>
    private static void AssertShare(Track track, double expected, string axis)
    {
        var length = axis == "height" ? track.ActualHeight : track.ActualWidth;
        var thumb = axis == "height" ? track.Thumb!.ActualHeight : track.Thumb!.ActualWidth;

        Assert.True(
            Math.Abs(thumb / length - expected) < 0.01,
            $"Бегунок занял {thumb} из {length} (ожидалось {expected:P1}), "
            + $"дорожка {track.Minimum}…{track.Maximum}, значение {track.Value}, видимая часть {track.ViewportSize}.");
    }

    /// <summary>Полоса нужной ориентации, уже измеренная и разложенная: без разметки шаблон не раскрывается.</summary>
    private static ScrollBar Полоса(Orientation orientation = Orientation.Vertical)
    {
        var scrollBar = new ScrollBar
        {
            Orientation = orientation,
            Minimum = 0,
            Maximum = 20,
            Value = 10,
            ViewportSize = 100,
            Style = Style()
        };

        var thickness = orientation == Orientation.Vertical ? new Size(12, Length) : new Size(Length, 12);

        scrollBar.Measure(thickness);
        scrollBar.Arrange(new Rect(thickness));
        scrollBar.UpdateLayout();

        return scrollBar;
    }

    private static Track ДорожкаПолосы(ScrollBar scrollBar)
    {
        scrollBar.ApplyTemplate();

        // GetTemplateChild у FrameworkElement закрыт, а FindName ищет то же самое по шаблону:
        // дорожка обязана называться PART_Track, иначе полоса молча остаётся без неё.
        var track = scrollBar.Template?.FindName("PART_Track", scrollBar) as Track;

        Assert.NotNull(track);

        return track!;
    }

    /// <summary>Неявный стиль полосы из <c>App.xaml</c> вместе с шаблонами, на которые он ссылается.</summary>
    private static Style Style()
    {
        AppResources.Ensure();

        var dictionary = (ResourceDictionary)XamlReader.Parse(Wrapped(TakeScrollBarDeclarations()));
        var style = dictionary.Values.OfType<Style>().SingleOrDefault(candidate => candidate.TargetType == typeof(ScrollBar));

        Assert.NotNull(style);

        return style!;
    }

    /// <summary>
    /// Объявления полосы подряд: от первого шаблона до конца неявного стиля. Брать по одному
    /// нельзя - стиль ссылается на шаблоны, а шаблон бегунка на стиль бегунка, и разобранные
    /// поодиночке они не разрешат ссылок.
    /// </summary>
    private static string TakeScrollBarDeclarations()
    {
        var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(AppRoot(), "App.xaml"));
        var start = xaml.IndexOf("<ControlTemplate x:Key=\"ScrollBarPageButtonTemplate\"", StringComparison.Ordinal);
        var style = xaml.IndexOf("TargetType=\"{x:Type ScrollBar}\"", StringComparison.Ordinal);

        if (start < 0 || style < 0)
        {
            throw new InvalidOperationException("В App.xaml нет объявлений полосы прокрутки.");
        }

        var end = xaml.IndexOf("</Style>", style, StringComparison.Ordinal);

        if (end < 0)
        {
            throw new InvalidOperationException("В App.xaml не закрыт неявный стиль полосы прокрутки.");
        }

        return xaml[start..(end + "</Style>".Length)];
    }

    /// <summary>
    /// Объявление обрамляется словарём: ссылки на цвета при разборе ищутся в словаре разметки,
    /// а не в словаре приложения, и без обрамления разбор падает на первой же из них.
    /// </summary>
    private static string Wrapped(string declaration)
        => "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
            + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
            + declaration
            + "</ResourceDictionary>";

    private static string AppRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "src", "AudioVisualizer.App");

            if (System.IO.File.Exists(System.IO.Path.Combine(candidate, "App.xaml")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Не найден каталог src\\AudioVisualizer.App.");
    }
}