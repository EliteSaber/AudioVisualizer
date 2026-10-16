using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AudioVisualizer.Tests.Assets;

/// <summary>
/// Иконка приложения и файлы, на которые ссылается проект.
/// </summary>
/// <remarks>
/// Рисунок иконки глазами не проверяется: правило из AGENTS.md запрещает рендеры и
/// скриншоты. Поэтому проверяются вещи, которые иначе всплыли бы уже у пользователя: файл
/// без нужных кадров обрезался бы в проводнике мыльным, а кадр без альфы лёг бы на панель
/// задач белым квадратом.
/// </remarks>
[Collection(WpfUi.Name)]
public partial class AppIconTests
{
    private static readonly int[] ExpectedSizes = [16, 24, 32, 48, 64, 128, 256];

    [GeneratedRegex("""<ApplicationIcon>\s*(?<path>[^<]+?)\s*</ApplicationIcon>""")]
    private static partial Regex ApplicationIconPattern();

    [Fact]
    public void Иконка_СодержитВсеНужныеРазмеры()
    {
        var sizes = Sta.Get(() => Read(IconPath()).Select(frame => frame.PixelWidth).ToArray());

        Assert.Equal(ExpectedSizes, sizes);
    }

    [Fact]
    public void Иконка_ПрозрачнаПоУглам()
    {
        Sta.Run(() =>
        {
            foreach (var frame in Read(IconPath()))
            {
                var pixels = Pixels(frame);

                // Угол скруглённого квадрата прозрачен: иначе Windows положила бы на панель
                // задач и в проводник прямоугольник во весь кадр.
                Assert.True(Alpha(pixels, frame.PixelWidth, 0, 0) == 0, $"Угол кадра {frame.PixelWidth} непрозрачен.");
                Assert.True(Alpha(pixels, frame.PixelWidth, frame.PixelWidth - 1, frame.PixelHeight - 1) == 0, $"Угол кадра {frame.PixelWidth} непрозрачен.");
            }
        });
    }

    [Fact]
    public void Иконка_НепрозрачнаВЦентре()
    {
        Sta.Run(() =>
        {
            foreach (var frame in Read(IconPath()))
            {
                var pixels = Pixels(frame);
                var x = frame.PixelWidth / 2;
                var y = frame.PixelHeight / 2;

                Assert.True(Alpha(pixels, frame.PixelWidth, x, y) == 255, $"Центр кадра {frame.PixelWidth} прозрачен — рисунка нет.");
            }
        });
    }

    [Fact]
    public void Иконка_НарисованаАНеЗалитаОднимЦветом()
    {
        Sta.Run(() =>
        {
            foreach (var frame in Read(IconPath()))
            {
                var pixels = Pixels(frame);

                // Один цвет на кадре означал бы, что рисунок не доехал: знак состоит из
                // градиента фона, рамки и столбиков, и даже без сглаживания в нём десятки
                // оттенков. Проверка вхолостую не проходит: пустой кадр даёт один ноль.
                var colors = new HashSet<int>();

                for (var index = 3; index < pixels.Length; index += 4)
                {
                    colors.Add(BitConverter.ToInt32(pixels, index - 3));

                    if (colors.Count > 8)
                    {
                        break;
                    }
                }

                Assert.True(colors.Count > 8, $"Кадр {frame.PixelWidth} залит одним цветом.");
            }
        });
    }

    [Fact]
    public void ApplicationIcon_ПрописанИФайлСуществует()
    {
        var project = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "AudioVisualizer.App", "AudioVisualizer.App.csproj"));
        var match = ApplicationIconPattern().Match(project);

        Assert.True(match.Success, "В csproj нет ApplicationIcon: иконка лежит в проекте, но в exe не попадает.");

        // Путь проверяется тем же чтением, каким ищет его MSBuild: относительный, от папки
        // проекта. Несовпадение регистра или слеша сломало бы сборку с невнятным сообщением.
        var declared = Path.GetFullPath(Path.Combine(
            RepositoryRoot(),
            "src",
            "AudioVisualizer.App",
            match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar)));

        Assert.True(File.Exists(declared), $"Файл иконки из csproj не найден: {declared}");
        Assert.Equal(IconPath(), declared, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Мастер_И_Логотип_Существуют()
    {
        var master = FilePath("src", "AudioVisualizer.App", "Assets", "AppIcon-1024.png");
        var logo = FilePath("docs", "logo.png");

        Assert.True(File.Exists(master), $"Мастер иконки не найден: {master}");
        Assert.True(File.Exists(logo), $"Логотип для README не найден: {logo}");

        Sta.Run(() =>
        {
            Assert.Equal(1024, Read(master)[0].PixelWidth);
            Assert.Equal(512, Read(logo)[0].PixelWidth);
        });
    }

    [Fact]
    public void Мастер_ПрозраченПоУглам()
    {
        Sta.Run(() =>
        {
            var frame = Read(FilePath("src", "AudioVisualizer.App", "Assets", "AppIcon-1024.png"))[0];

            Assert.True(Alpha(Pixels(frame), frame.PixelWidth, 0, 0) == 0, "Угол мастера непрозрачен.");
        });
    }

    /// <summary>Кадры файла, прочитанные тем же декодером, которым пользуется Windows.</summary>
    private static BitmapFrame[] Read(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        try
        {
            return decoder.Frames.Cast<BitmapFrame>().Select(Frame =>
            {
                // Замороженный кадр можно читать с любого потока, а сама коллекция остаётся
                // привязанной к потоку, который её создал.
                Frame.Freeze();

                return Frame;
            }).ToArray();
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>Пиксели кадра в виде BGRA: так их отдаёт копирование из снимка WPF.</summary>
    private static byte[] Pixels(BitmapSource frame)
    {
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        var data = new byte[width * height * 4];

        frame.CopyPixels(data, width * 4, 0);

        return data;
    }

    private static byte Alpha(byte[] pixels, int width, int x, int y) => pixels[((y * width) + x) * 4 + 3];

    private static string IconPath() => FilePath("src", "AudioVisualizer.App", "Assets", "AppIcon.ico");

    private static string FilePath(params string[] parts) => Path.Combine([RepositoryRoot(), .. parts]);

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